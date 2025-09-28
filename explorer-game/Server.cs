namespace ExplorerGame.Net;

using System.Net;
using System.Text;
using ExplorerGame.Core;
using ExplorerGame.Base;
using ExplorerGame.ConsoleVisualizer;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// Event args for a successful session connection, including sanitized client/user info,
/// the chosen session identifier, optional local session reference, and the response payload.
/// </summary>
public class SessionConnectedEventArgs : EventArgs
{
    /// <summary>
    /// Username provided by the client (sanitized and possibly truncated).
    /// </summary>
    public string ClientUsername { get; set; }

    /// <summary>
    /// Client network identifier (usually IP:port).
    /// </summary>
    public string ClientID { get; set; }

    /// <summary>
    /// Identifier used for this session
    /// </summary>
    public SessionIdentifier? SessionIdentifier { get; set; }

    /// <summary>
    /// Local game session created for the client, if any.
    /// </summary>
    public LocalGameSession? GameSession { get; set; }

    /// <summary>
    /// Raw JSON response returned to the client during connect.
    /// </summary>
    public JObject Response { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionConnectedEventArgs"/> class
    /// with the specified client information, session identifier, optional local session,
    /// and response payload.
    /// </summary>
    /// <param name="clientUsername">The sanitized username provided by the client.</param>
    /// <param name="clientID">The network identifier of the client (usually IP:port).</param>
    /// <param name="sessionIdentifier">The session identifier assigned to the client.</param>
    /// <param name="session">The local game session created for the client, if any.</param>
    /// <param name="response">The JSON response returned to the client.</param>
    public SessionConnectedEventArgs(string clientUsername, string clientID, SessionIdentifier? sessionIdentifier, LocalGameSession? session, JObject response)
    {
        ClientUsername = clientUsername;
        ClientID = clientID;
        SessionIdentifier = sessionIdentifier;
        Response = response;
        GameSession = session;
    }
}

/// <summary>
/// Handles HTTP requests for connecting clients and moving agents.
/// Manages per-client session limits, session lifecycle, and visualization updates.
/// </summary>
public class ConnectionHandler
{
    private readonly ConsoleVisualizer? visualizer;
    private readonly Tile?[,] map;
    private readonly object sync = new object();

    /// <summary>
    /// Per-client list of session IDs.
    /// </summary>
    private readonly Dictionary<string, List<string>> clientSessions = new();

    /// <summary>
    /// Number of active sessions
    /// </summary>
    public int SessionCount => sessionsById.Count;

    /// <summary>
    /// Global map of sessionId -> session wrapper.
    /// </summary>
    private readonly Dictionary<string, SessionWrapper> sessionsById = new();

    /// <summary>
    /// Idle timeout for killing inactive sessions.
    /// </summary>
    private readonly TimeSpan idleTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Cooldown between actions per session.
    /// </summary>
    private readonly TimeSpan sessionActionCooldown = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Raised after a successful /connect; includes sanitized username and created session data.
    /// </summary>
    public event EventHandler<SessionConnectedEventArgs>? SessionConnected;

    /// <summary>
    /// Creates a handler bound to a specific map; optionally hooks a visualizer for live updates.
    /// </summary>
    public ConnectionHandler(Tile?[,] map, ConsoleVisualizer? visualizer)
    {
        this.map = map;
        this.visualizer = visualizer;
    }

    /// <summary>
    /// Starts an HTTP server loop on the given port. Processes /connect and /move POST requests.
    /// Also runs a cleanup loop for idle sessions.
    /// </summary>
    public async Task StartHttpServer(int port, CancellationToken token)
    {
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://+:{port}/");
        listener.Start();

        _ = Task.Run(() => CleanupLoop(token), token);

        while (!token.IsCancellationRequested)
        {
            var context = await listener.GetContextAsync();
            if (context.Request.HttpMethod != "POST")
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
                continue;
            }

            JObject response;
            try
            {
                string clientID = context.Request.RemoteEndPoint?.ToString() ?? "unknown";

                // Read body with a short timeout to avoid hanging connections.
                using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                string body = await reader.ReadToEndAsync(cts.Token).WaitAsync(cts.Token);

                var args = JObject.Parse(body);
                args["clientId"] = clientID;

                // If sessionId is missing or unknown, route to HandleCommand directly.
                if (!args.ContainsKey("sid"))
                {
                    await SendResponse(HandleCommand(args, context), context);
                }
                else
                {
                    string? sessionId = args.Value<string>("sid");
                    if (sessionId == null || !sessionsById.TryGetValue(sessionId, out SessionWrapper? session) || session == null)
                    {
                        await SendResponse(HandleCommand(args, context), context);
                    }
                    else
                    {
                        // Serialize actions per session by chaining continuations on ActionQueue.
                        lock (sync)
                        {
                            session.ActionQueue = session.ActionQueue
                                .ContinueWith(
                                    _ => HandleCommandWithCooldown(args, context),
                                    CancellationToken.None,
                                    TaskContinuationOptions.None,
                                    TaskScheduler.Default
                                )
                                .Unwrap();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                response = new JObject
                {
                    ["success"] = false,
                    ["message"] = "Exception occured during request processing: " + ex.Message
                };
                await SendResponse(response, context);
            }
        }

        listener.Stop();
    }

    /// <summary>
    /// Handles a single command and enforces a small cooldown before sending the response.
    /// Used to throttle per-session action rate.
    /// </summary>
    private async Task HandleCommandWithCooldown(JObject args, HttpListenerContext context)
    {
        JObject response;
        try
        {
            response = HandleCommand(args, context);
        }
        catch (Exception ex)
        {
            response = new JObject
            {
                ["success"] = false,
                ["message"] = "Exception occured during request processing: " + ex.Message
            };
        }
        await Task.Delay(sessionActionCooldown);
        await SendResponse(response, context);
    }

    /// <summary>
    /// Writes a JSON response and closes the response stream.
    /// </summary>
    private async Task SendResponse(JObject response, HttpListenerContext context)
    {
        var responseString = response.ToString(Formatting.None);
        var buffer = Encoding.UTF8.GetBytes(responseString);
        context.Response.ContentType = "application/json";
        context.Response.ContentEncoding = Encoding.UTF8;
        context.Response.ContentLength64 = buffer.Length;
        await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        context.Response.OutputStream.Flush();
        context.Response.Close();
    }

    /// <summary>
    /// Command router for /connect and /move endpoints.
    /// Applies username sanitization for connect, and forwards moves to the target session.
    /// </summary>
    private JObject HandleCommand(JObject args, HttpListenerContext context)
    {
        JObject response;

        switch (context.Request.Url?.AbsolutePath)
        {
            case "/connect":
                (response, string? sessionId) = HandleConnect(args);

                SessionWrapper? session = null;
                if (response.Value<bool>("success"))
                    session = sessionsById[response.Value<string>("sid") ?? throw new Exception("Missing 'sid' in response")];

                string username = args.Value<string>("username")?.Trim() ?? "unknown";
                username = Regex.Replace(username, @"\s+", " ");   // collapse whitespace
                username = Regex.Replace(username, @"\p{C}", "");   // remove control chars
                if (username.Length > 15)
                    username = username.Substring(0, 12) + "...";

                SessionConnected?.Invoke(this, new
                (
                    username,
                    args.Value<string>("clientId") ?? throw new Exception("clientId missing"),
                    session?.SessionIdentifier,
                    session?.Session,
                    response
                ));
                break;

            case "/move":
                (response, _) = HandleMove(args);
                break;

            default:
                response = new JObject { ["success"] = false, ["message"] = "Unknown request" };
                break;
        }
        return response;
    }

    /// <summary>
    /// Handles the /connect request. Validates identifier uniqueness, per-client session limits,
    /// creates a LocalGameSession, and attaches it to the visualizer if present.
    /// </summary>
    private (JObject, string? sessionId) HandleConnect(JObject args)
    {
        string clientId = args.Value<string>("clientId")!;
        JObject? vsid = args.Value<JObject>("vsid");
        if (visualizer != null && vsid == null)
            return (new JObject { ["success"] = false, ["message"] = "This server requieres VSID to connect. None present." }, null);

        ConsoleColor? color = vsid == null ? null : Enum.Parse<ConsoleColor>(vsid.Value<string>("color")!);
        string? identifier = vsid?.Value<string>("identifierStr")!;
        if (vsid != null)
        {
            identifier = Regex.Replace(identifier, @"\s+", " ");
            identifier = Regex.Replace(identifier, @"\p{C}", "");
        }

        lock (sync)
        {
            // Prevent identical identifier/color collisions across sessions.
            if (vsid != null && sessionsById.Values.Any(id => id.SessionIdentifier.IdentifierStr == identifier && id.SessionIdentifier.Color == color))
                return (new JObject { ["success"] = false, ["message"] = "Identifier already in use" }, null);

            if (!clientSessions.ContainsKey(clientId))
                clientSessions[clientId] = new();

            // Limit sessions per client to avoid abuse.
            if (clientSessions[clientId].Count >= 20)
                return (new JObject { ["success"] = false, ["message"] = "Too many sessions" }, null);

            var session = new LocalGameSession(map);
            session.AgentDied += AgentDied;

            string uuid = Guid.NewGuid().ToString();
            SessionIdentifier sid = new SessionIdentifier
            (
                vsid == null ? null : new VisualSessionIdentifier(identifier, color!.Value, map),
                uuid
            );

            sessionsById[uuid] = new SessionWrapper(clientId, session, sid, DateTime.UtcNow);
            clientSessions[clientId].Add(uuid);

            if(visualizer != null)
                visualizer.AttachGameSession(session, sid.VSID!);

            return (new JObject { ["success"] = true, ["sid"] = uuid }, uuid);
        }
    }

    /// <summary>
    /// Removes a session from both global and per-client indices.
    /// </summary>
    private void ForgetSession(string sessionId, string? clientID = null)
    {
        if (clientID == null)
            clientID = sessionsById[sessionId].ClientID;
        clientSessions[clientID].RemoveAll(x => x == sessionId);
        sessionsById.Remove(sessionId);
    }

    /// <summary>
    /// Listener for agent death. Removes the session that owns the dead agent.
    /// </summary>
    private void AgentDied(object? sender, AgentDiedEventArgs e)
    {
        if (sender is not LocalGameSession session || sender == null)
            return;

        var sessionKeyValue = sessionsById.First(x => x.Value.Session == session);
        ForgetSession(sessionKeyValue.Key, sessionKeyValue.Value.ClientID);
    }

    /// <summary>
    /// Handles the /move request. Validates session existence, forwards the move,
    /// and updates last activity if the move executed and the agent is alive.
    /// </summary>
    private (JObject, string? sessionId) HandleMove(JObject request)
    {
        string sessionId = request.Value<string>("sid")!;
        int dx = request.Value<int>("dx");
        int dy = request.Value<int>("dy");

        lock (sync)
        {
            if (!sessionsById.TryGetValue(sessionId, out var session))
                return (new JObject { ["success"] = false, ["message"] = "No living agent with requested session ID" }, null);

            MovementResult result = session.Session.Move(new Vector(dx, dy));
            if (result.IsAgentAlive && result.MovedSuccessfully)
                UpdateActivity(sessionId);

            return
            (
                new JObject
                {
                    ["success"] = true,
                    ["moved"] = result.MovedSuccessfully,
                    ["alive"] = result.IsAgentAlive,
                    ["discovered"] = Tile.Serialize(result.DiscoveredTile)
                },
                sessionId
            );
        }
    }

    /// <summary>
    /// Updates the last-activity timestamp for a session.
    /// </summary>
    private void UpdateActivity(string sessionId)
    {
        sessionsById[sessionId].LastActivity = DateTime.UtcNow;
    }

    /// <summary>
    /// Periodically kills sessions that have been idle longer than <see cref="idleTimeout"/>.
    /// </summary>
    public async Task CleanupLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await Task.Delay(1000, token);

            lock (sync)
            {
                DateTime now = DateTime.UtcNow;
                var inactiveSessions = sessionsById.Where(x => now - x.Value.LastActivity > idleTimeout);
                foreach (var session in inactiveSessions)
                    session.Value.Session.Kill("Inactive for too long");
            }
        }
    }

    /// <summary>
    /// Wraps a LocalGameSession with metadata used by the server.
    /// </summary>
    private class SessionWrapper
    {
        public string ClientID { get; set; }
        public LocalGameSession Session { get; set; }
        public SessionIdentifier SessionIdentifier { get; set; }
        public DateTime LastActivity { get; set; }

        // Used to serialize per-session actions in arrival order.
        public Task ActionQueue = Task.CompletedTask;

        public SessionWrapper(string clientID, LocalGameSession session, SessionIdentifier sessionIdentifier, DateTime lastActivity)
        {
            ClientID = clientID;
            Session = session;
            SessionIdentifier = sessionIdentifier;
            LastActivity = lastActivity;
        }
    }
}
