using ExplorerGame.Core;

namespace ExplorerGame.Base;

public interface IGameSession
{
    public bool IsAgentAlive { get; }
    public Tile? DiscoveredTile { get; }
    public MovementResult Move(Vector move);
}

public class AgentMovementEventArgs : EventArgs
{
    public Vector AgentCurrentLocation { get; set; }
    public Vector AgentPreviousLocation { get; set; }

    public AgentMovementEventArgs(Vector agentCurrentLocation, Vector agentPreviousLocation)
    {
        AgentCurrentLocation = agentCurrentLocation;
        AgentPreviousLocation = agentPreviousLocation;
    }
}

public class LocalGameSession : IGameSession
{
    private bool isAgentAlive;
    public bool IsAgentAlive
    {
        get => isAgentAlive;
        set
        {
            isAgentAlive = value;
            if (!value)
                AgentDied?.Invoke(this, EventArgs.Empty);
        }
    }
    public Tile? DiscoveredTile { get; private set; }

    public event EventHandler? AgentDied;
    internal event EventHandler<AgentMovementEventArgs>? AgentMoved;

    internal Tile?[,] map;
    private Vector agentLocation;
    internal Vector AgentLocation
    {
        get => agentLocation;
        private set
        {
            Vector prevLoc = agentLocation;
            agentLocation = value;
            AgentMoved?.Invoke(this, new AgentMovementEventArgs(agentLocation, prevLoc));
        }
    }

    internal static readonly HashSet<Vector> VALID_MOVES = new HashSet<Vector>
        {
            new (0, 1),
            new (0, -1),
            new (1, 0),
            new (-1, 0),
            new (0, 2),
            new (0, -2),
            new (2, 0),
            new (-2, 0)
        };

    internal LocalGameSession(Tile?[,] map)
    {
        this.map = map;
        IsAgentAlive = true;
    }

    internal bool Translate(Vector delta)
    {
        AgentLocation += delta;

        // Check if agent encountered a tile
        if (map[AgentLocation.X, AgentLocation.Y].HasValue)
        {
            DiscoveredTile = map[AgentLocation.X, AgentLocation.Y];
            IsAgentAlive = false;
        }

        return IsAgentAlive;
    }

    public MovementResult Move(Vector move)
    {
        if (!IsAgentAlive)
            return new MovementResult(false, false);

        if (!VALID_MOVES.Contains(move))
            return new MovementResult(false, true);

        return new MovementResult(true, Translate(move));
    }
}