namespace ExplorerGame.Net;

using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ExplorerGame.Core;
using ExplorerGame.ConsoleVisualizer;

public class RemoteGameSession : IGameSession
{
    private readonly string sessionId;
    private readonly string serverUrl;
    private readonly HttpClient httpClient = new();

    public string? LastResponseMessage { get; private set; }

    public RemoteGameSession(string serverUrl, string sessionId)
    {
        this.serverUrl = serverUrl.TrimEnd('/');
        this.sessionId = sessionId;
    }

    public bool IsAgentAlive { get; private set; } = true;
    public Tile? DiscoveredTile => null;

    public MovementResult Move(Vector move)
    {
        var request = new
        {
            sessionId,
            dx = move.X,
            dy = move.Y
        };

        var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
        var response = httpClient.PostAsync(serverUrl + "/move", content).Result;
        return HandleMoveResponse(response);
    }

    public AsyncMovementResult MoveAsync(Vector move)
    {
        var request = new
        {
            sessionId,
            dx = move.X,
            dy = move.Y
        };

        var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
        AsyncMovementResult result = new();
        var responseTask = httpClient.PostAsync(serverUrl + "/move", content);
        result.ResponseHandlerTask = responseTask.ContinueWith(x =>
        {
            result.MovementResult = HandleMoveResponse(x.Result);
            result.Ready = true;
        });
        return result;
    }

    private MovementResult HandleMoveResponse(HttpResponseMessage response)
    {
        var json = response.Content.ReadAsStringAsync().Result;
        var result = JObject.Parse(json);

        if (result.ContainsKey("message"))
            LastResponseMessage = result.Value<string>("message");
        else
            LastResponseMessage = null;

        if (!result.Value<bool>("success"))
            return new MovementResult(false, IsAgentAlive);

        IsAgentAlive = result.Value<bool>("alive");
        return new MovementResult(true, IsAgentAlive);
    }
}

public class RemoteGameSessionFactory
{
    private readonly string serverUrl;
    private readonly string username;
    private readonly HttpClient httpClient = new();

    public RemoteGameSessionFactory(string serverUrl, string username = "unknown")
    {
        this.serverUrl = serverUrl.TrimEnd('/');
        this.username = username;
    }

    public RemoteGameSession Create(SessionIdentifier identifier)
    {
        JObject request = new JObject()
        {
            ["identifier"] = identifier.Identifier,
            ["color"] = identifier.Color.ToString(),
            ["username"] = username
        };

        var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
        var response = httpClient.PostAsync(serverUrl + "/connect", content).Result;
        var json = response.Content.ReadAsStringAsync().Result;
        var obj = JObject.Parse(json);

        if (!obj.Value<bool>("success"))
            throw new Exception(obj["message"]?.ToString() ?? "Unknown error");

        var uuid = obj.Value<string>("uuid") ?? throw new Exception("Invalid response from server");
        return new RemoteGameSession(serverUrl, uuid);
    }
}
