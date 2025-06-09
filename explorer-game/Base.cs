using ExplorerGame.Core;

namespace ExplorerGame.Base;

public class AgentDiedEventArgs : EventArgs
{
    public string DeathReason { get; set; }

    public AgentDiedEventArgs(string reason)
    {
        DeathReason = reason;
    }
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
    }
    public Tile? DiscoveredTile { get; private set; }

    public event EventHandler<AgentDiedEventArgs>? AgentDied;
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
        new (0, 0),
        new (0, 1),
        new (0, -1),
        new (1, 0),
        new (-1, 0),
        new (0, 2),
        new (0, -2),
        new (2, 0),
        new (-2, 0)
    };

    public LocalGameSession(Tile?[,] map)
    {
        this.map = map;
        isAgentAlive = true;
    }

    internal bool Translate(Vector delta)
    {
        AgentLocation += delta;

        // Check if agent encountered a tile
        if (AgentLocation.X < 0 || AgentLocation.X >= map.GetLength(0) ||
            AgentLocation.Y < 0 || AgentLocation.Y >= map.GetLength(1))
        {
            Kill("Wandered out of the map");
        }
        else if (map[AgentLocation.X, AgentLocation.Y].HasValue)
        {
            DiscoveredTile = map[AgentLocation.X, AgentLocation.Y];
            Kill("Stepped on a trap");
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

    public void Kill(string reason)
    {
        isAgentAlive = false;
        AgentDied?.Invoke(this, new AgentDiedEventArgs(reason));
    }
}