namespace ExplorerGame.Net;

using System.Net;
using System.Net.Sockets;
using System.Text;
using ExplorerGame.Core;
using ExplorerGame.Base;
using ExplorerGame.ConsoleVisualizer;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

    private readonly Dictionary<string, List<(string sessionId, DateTime lastActivity)>> clientSessions = new();
    private readonly Dictionary<string, LocalGameSession> sessionsById = new();
    private readonly Dictionary<string, SessionIdentifier> sessionIdentifiers = new();
    private readonly TimeSpan idleTimeout = TimeSpan.FromSeconds(15);
    
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

            // TODO wait 200ms after each move
            var responseString = response.ToString(Formatting.None);
            var buffer = Encoding.UTF8.GetBytes(responseString);
            context.Response.ContentType = "application/json";
            context.Response.ContentEncoding = Encoding.UTF8;
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            context.Response.Close();
        }

        listener.Stop();
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
                    gameSession = sessionsById[response.Value<string>("uuid") ?? throw new Exception("WHAT :o")];
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
            if (sessionIdentifiers.Values.Any(id => id.Identifier == identifier && id.Color == color))
                return (new JObject { ["success"] = false, ["message"] = "Identifier already in use" }, null);

            if (!clientSessions.ContainsKey(clientId))
                clientSessions[clientId] = new();

            if (clientSessions[clientId].Count >= 5)
                return (new JObject { ["success"] = false, ["message"] = "Too many sessions" }, null);

            var session = new LocalGameSession(map);
            string sessionId = Guid.NewGuid().ToString();
            var sid = new SessionIdentifier(identifier, color, map);

            sessionsById[sessionId] = session;
            sessionIdentifiers[sessionId] = sid;
            clientSessions[clientId].Add((sessionId, DateTime.UtcNow));

            visualizer.AttachGameSession(session, sid);
            return (new JObject { ["success"] = true, ["uuid"] = sessionId }, sid);
        }
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

            var result = session.Move(new Vector(dx, dy));
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
        foreach (var (key, list) in clientSessions)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].sessionId == sessionId)
                {
                    list[i] = (sessionId, DateTime.UtcNow);
                    return;
                }
            }
        }
    }

    public async Task CleanupLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await Task.Delay(5000, token);

            lock (sync)
            {
                foreach (var client in clientSessions)
                {
                    for (int i = client.Value.Count - 1; i >= 0; i--)
                    {
                        var (sessionId, lastTime) = client.Value[i];
                        if (DateTime.UtcNow - lastTime > idleTimeout)
                        {
                            if (sessionsById.TryGetValue(sessionId, out var session))
                                session.Kill("Inactive for too long");

                            client.Value.RemoveAt(i);
                            sessionsById.Remove(sessionId);
                            sessionIdentifiers.Remove(sessionId);
                        }
                    }
                }
            }
        }
    }
    
}
