namespace ExplorerGame.Net;

using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ExplorerGame.Core;
using ExplorerGame.ConsoleVisualizer;

/// <summary>
/// Represents a remote game session connected to a server.
/// Implements <see cref="IGameSession"/> but communicates moves
/// via HTTP requests to a backend service.
/// </summary>
public class RemoteGameSession : IGameSession
{
    private readonly string sessionId;
    private readonly string serverUrl;
    private readonly HttpClient httpClient = new();

    /// <summary>
    /// The last response message received from the server, if any.
    /// </summary>
    public string? LastResponseMessage { get; private set; }

    /// <summary>
    /// Creates a new remote game session associated with a server and session ID.
    /// </summary>
    /// <param name="serverUrl">Base URL of the server.</param>
    /// <param name="sessionId">Unique session identifier assigned by the server.</param>
    public RemoteGameSession(string serverUrl, string sessionId)
    {
        this.serverUrl = serverUrl.TrimEnd('/');
        this.sessionId = sessionId;
    }

    /// <inheritdoc/>
    public bool IsAgentAlive { get; private set; } = true;

    /// <inheritdoc/>
    
    /// <summary>
    /// ! NOT YET IMPLEMENTED !
    /// Remote session does not expose discovered tiles directly.
    /// TODO implement
    /// </summary>
    public Tile? DiscoveredTile => throw new NotImplementedException();

    /// <summary>
    /// Sends a synchronous move request to the server and processes the result.
    /// The movent of an agent takes some time. This method waits until the movement has
    /// finished and then returns.
    /// </summary>
    /// <param name="move">Movement vector to attempt.</param>
    /// <returns>The movement result as reported by the server.</returns>
    public MovementResult Move(Vector move)
    {
        var request = new
        {
            sid = sessionId,
            dx = move.X,
            dy = move.Y
        };

        var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
        var response = httpClient.PostAsync(serverUrl + "/move", content).Result;
        return HandleMoveResponse(response);
    }

    /// <summary>
    /// Sends an asynchronous move request to the server and processes the result when ready.
    /// The movent of an agent takes some time. This method finishes immidiately and returns
    /// an async result handle, which will be updated when the move is finished.
    /// </summary>
    /// <param name="move">Movement vector to attempt.</param>
    /// <returns>
    /// An <see cref="AsyncMovementResult"/> -> async result handle, which will be automatically
    /// updated when the move finishes
    /// </returns>
    public AsyncMovementResult MoveAsync(Vector move)
    {
        var request = new
        {
            sid = sessionId,
            dx = move.X,
            dy = move.Y
        };

        var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
        AsyncMovementResult result = new();
        var responseTask = httpClient.PostAsync(serverUrl + "/move", content);

        // Once the server responds, parse the response and update the result
        result.ResponseHandlerTask = responseTask.ContinueWith(x =>
        {
            result.MovementResult = HandleMoveResponse(x.Result);
            result.Ready = true;
        });

        return result;
    }

    /// <summary>
    /// Handles parsing of a move response from the server.
    /// Updates internal state such as <see cref="IsAgentAlive"/> and <see cref="LastResponseMessage"/>.
    /// </summary>
    /// <param name="response">The HTTP response received from the server.</param>
    /// <returns>A <see cref="MovementResult"/> indicating success and survival state.</returns>
    private MovementResult HandleMoveResponse(HttpResponseMessage response)
    {
        var json = response.Content.ReadAsStringAsync().Result;
        var result = JObject.Parse(json);

        if (result.ContainsKey("message"))
            LastResponseMessage = result.Value<string>("message");
        else
            LastResponseMessage = null;

        if (!result.Value<bool>("success"))
        {
            IsAgentAlive = false;
            return new MovementResult(false, IsAgentAlive);
        }

        IsAgentAlive = result.Value<bool>("alive");
        return new MovementResult(result.Value<bool>("moved"), IsAgentAlive);
    }
}

/// <summary>
/// Factory for creating <see cref="RemoteGameSession"/> instances.
/// Handles the initial connection and registration with the remote server.
/// </summary>
public class RemoteGameSessionFactory
{
    private readonly string serverUrl;
    private readonly string username;
    private readonly HttpClient httpClient = new();

    /// <summary>
    /// Initializes a new factory for connecting to a given server.
    /// </summary>
    /// <param name="serverUrl">Base URL of the server.</param>
    /// <param name="username">Optional username to associate with the session.</param>
    public RemoteGameSessionFactory(string serverUrl, string username = "unknown")
    {
        this.serverUrl = serverUrl.TrimEnd('/');
        this.username = username;
    }

    /// <summary>
    /// Creates a new <see cref="RemoteGameSession"/> by sending a connect request to the server.
    /// </summary>
    /// <param name="identifier">The session identifier to register with the server.</param>
    /// <returns>A new <see cref="RemoteGameSession"/> bound to the created session.</returns>
    /// <exception cref="Exception">
    /// Thrown if the server rejects the connection or returns invalid data.
    /// </exception>
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

        var uuid = obj.Value<string>("sid") ?? throw new Exception("Invalid response from server");
        return new RemoteGameSession(serverUrl, uuid);
    }
}
