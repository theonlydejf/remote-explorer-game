namespace ExplorerGame.Net;

using System.Net;
using System.Net.Sockets;
using System.Text;
using ExplorerGame.Core;
using ExplorerGame.Base;
using ExplorerGame.ConsoleVisualizer;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography.X509Certificates;

public class SessionConnectedEventArgs : EventArgs
{
    public string ClientUsername { get; set; }
    public string ClientID { get; set; }
    public SessionIdentifier? SessionIdentifier { get; set; }
    public LocalGameSession? GameSession { get; set; }
    public JObject Response { get; set; }

    public SessionConnectedEventArgs(string clientUsername, string clientID, SessionIdentifier? sessionIdentifier, LocalGameSession? session, JObject response)
    {
        ClientUsername = clientUsername;
        ClientID = clientID;
        SessionIdentifier = sessionIdentifier;
        Response = response;
        GameSession = session;
    }
}

public class ConnectionHandler
{
    private readonly ConsoleVisualizer visualizer;
    private readonly Tile?[,] map;
    private readonly object sync = new object();

    private readonly Dictionary<string, List<string>> clientSessions = new();
    private readonly Dictionary<string, SessionWrapper> sessionsById = new();
    private readonly TimeSpan idleTimeout = TimeSpan.FromSeconds(5);
    private readonly TimeSpan moveDuration = TimeSpan.FromMilliseconds(200);

    public event EventHandler<SessionConnectedEventArgs>? SessionConnected;

    public ConnectionHandler(Tile?[,] map, ConsoleVisualizer visualizer)
    {
        this.map = map;
        this.visualizer = visualizer;
    }

    public async Task StartHttpServer(string prefix, CancellationToken token)
    {
        var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
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
                response = await HandleCommand(context);
            }
            catch (Exception ex)
            {
                response = new JObject
                {
                    ["success"] = false,
                    ["message"] = "Exception occured during request processing: " + ex.Message
                };
            }

            await SendResponse(response, context);
        }

        listener.Stop();
    }

    private async Task SendResponse(JObject response, HttpListenerContext context)
    {
        var responseString = response.ToString(Formatting.None);
        var buffer = Encoding.UTF8.GetBytes(responseString);
        context.Response.ContentType = "application/json";
        context.Response.ContentEncoding = Encoding.UTF8;
        context.Response.ContentLength64 = buffer.Length;
        await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        context.Response.Close();
    }

    private async Task<JObject> HandleCommand(HttpListenerContext context)
    {
        string clientID = context.Request.RemoteEndPoint?.ToString() ?? "unknown";

        using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
        string body = await reader.ReadToEndAsync();
        var args = JObject.Parse(body);
        args["clientId"] = clientID;
        JObject response;

        switch (context.Request.Url?.AbsolutePath)
        {
            case "/connect":
                (response, SessionIdentifier? sid) = HandleConnect(args);
                LocalGameSession? gameSession = null;
                if (response.Value<bool>("success"))
                    gameSession = sessionsById[response.Value<string>("uuid") ?? throw new Exception("WHAT :o")].Session;
                SessionConnected?.Invoke(this, new(args.Value<string>("username") ?? "unknown", clientID, sid, gameSession, response));
                break;
            case "/move":
                response = HandleMove(args);
                break;
            default:
                response = new JObject { ["success"] = false, ["message"] = "Unknown request" };
                break;
        }
        return response;
    }

    private (JObject, SessionIdentifier?) HandleConnect(JObject args)
    {
        string clientId = args.Value<string>("clientId")!;
        string identifier = args.Value<string>("identifier")!;
        ConsoleColor color = Enum.Parse<ConsoleColor>(args.Value<string>("color")!);

        lock (sync)
        {
            if (sessionsById.Values.Any(id => id.SessionIdentifier.Identifier == identifier && id.SessionIdentifier.Color == color))
                return (new JObject { ["success"] = false, ["message"] = "Identifier already in use" }, null);

            if (!clientSessions.ContainsKey(clientId))
                clientSessions[clientId] = new();

            if (clientSessions[clientId].Count >= 5)
                return (new JObject { ["success"] = false, ["message"] = "Too many sessions" }, null);

            var session = new LocalGameSession(map);
            session.AgentDied += AgentDied;
            string sessionId = Guid.NewGuid().ToString();
            var sid = new SessionIdentifier(identifier, color, map);

            sessionsById[sessionId] = new SessionWrapper(clientId, session, sid, DateTime.UtcNow);
            clientSessions[clientId].Add(sessionId);

            visualizer.AttachGameSession(session, sid);
            return (new JObject { ["success"] = true, ["uuid"] = sessionId }, sid);
        }
    }

    private void ForgetSession(string sessionId, string? clientID = null)
    {
        if (clientID == null)
            clientID = sessionsById[sessionId].ClientID;
        clientSessions[clientID].RemoveAll(x => x == sessionId);
        sessionsById.Remove(sessionId);
    }

    private void AgentDied(object? sender, AgentDiedEventArgs e)
    {
        if (sender is not LocalGameSession session || sender == null)
            return;

        var sessionKeyValue = sessionsById.First(x => x.Value.Session == session);
        ForgetSession(sessionKeyValue.Key, sessionKeyValue.Value.ClientID);
    }

    private JObject HandleMove(JObject request)
    {
        string sessionId = request.Value<string>("sessionId")!;
        int dx = request.Value<int>("dx");
        int dy = request.Value<int>("dy");

        lock (sync)
        {
            if (!sessionsById.TryGetValue(sessionId, out var session))
                return new JObject { ["success"] = false, ["message"] = "Unknown sessionId" };

            var result = session.Session.Move(new Vector(dx, dy));
            if(result.IsPlayerAlive && result.MovedSuccessfully)
                UpdateActivity(sessionId);
            return new JObject
            {
                ["success"] = true,
                ["moved"] = result.MovedSuccessfully,
                ["alive"] = result.IsPlayerAlive
            };
        }
    }

    private void UpdateActivity(string sessionId)
    {
        sessionsById[sessionId].LastActivity = DateTime.UtcNow;
    }

    public async Task CleanupLoop(CancellationToken token)
    {
        return;
        while (!token.IsCancellationRequested)
        {
            await Task.Delay(1000, token);

            lock (sync)
            {
                DateTime now = DateTime.UtcNow;
                var inactiveSessions = sessionsById.Where(x => now - x.Value.LastActivity > idleTimeout);
                foreach (var session in inactiveSessions)
                {
                    session.Value.Session.Kill("Inactive for too long");
                    ForgetSession(session.Key, session.Value.ClientID);
                }
            }
        }
    }

    private class SessionWrapper
    {
        public string ClientID { get; set; }
        public LocalGameSession Session { get; set; }
        public SessionIdentifier SessionIdentifier { get; set; }
        public DateTime LastActivity { get; set; }

        public SessionWrapper(string clientID, LocalGameSession session, SessionIdentifier sessionIdentifier, DateTime lastActivity)
        {
            ClientID = clientID;
            Session = session;
            SessionIdentifier = sessionIdentifier;
            LastActivity = lastActivity;
        }
    }
}
