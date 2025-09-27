using ExplorerGame.Core;

namespace ExplorerGame.Base;

/// <summary>
/// Provides information about why an agent died.
/// </summary>
public class AgentDiedEventArgs : EventArgs
{
    /// <summary>
    /// Reason why the agent died.
    /// </summary>
    public string DeathReason { get; set; }

    public AgentDiedEventArgs(string reason)
    {
        DeathReason = reason;
    }
}

/// <summary>
/// Provides information about an agent's movement,
/// including both the previous and current locations.
/// </summary>
public class AgentMovementEventArgs : EventArgs
{
    /// <summary>
    /// Current location of the agent after moving.
    /// </summary>
    public Vector AgentCurrentLocation { get; set; }

    /// <summary>
    /// Previous location of the agent before moving.
    /// </summary>
    public Vector AgentPreviousLocation { get; set; }

    public AgentMovementEventArgs(Vector agentCurrentLocation, Vector agentPreviousLocation)
    {
        AgentCurrentLocation = agentCurrentLocation;
        AgentPreviousLocation = agentPreviousLocation;
    }
}

/// <summary>
/// Represents a session in which a single agent explores a known map.
/// Manages agent state, movement, death conditions, and raises events when
/// the agent moves or dies.
/// </summary>
public class LocalGameSession : IGameSession
{
    private bool isAgentAlive;

    /// <summary>
    /// Gets whether the agent is still alive.
    /// </summary>
    public bool IsAgentAlive => isAgentAlive;

    /// <summary>
    /// Tile that caused the agent’s death, if applicable.
    /// </summary>
    public Tile? DiscoveredTile { get; private set; }

    /// <summary>
    /// Event raised when the agent dies, providing the reason.
    /// </summary>
    public event EventHandler<AgentDiedEventArgs>? AgentDied;

    /// <summary>
    /// Event raised when the agent moves, providing old and new positions.
    /// Internal because it is primarily consumed by the visualizer.
    /// </summary>
    internal event EventHandler<AgentMovementEventArgs>? AgentMoved;

    /// <summary>
    /// Reference to the map being explored.
    /// </summary>
    internal Tile?[,] map;

    private Vector agentLocation;

    /// <summary>
    /// Current agent location on the map. 
    /// Setting this property triggers the <see cref="AgentMoved"/> event.
    /// </summary>
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

    /// <summary>
    /// Set of all valid moves the agent can perform.
    /// Includes single steps, double steps, and the option to stay in place.
    /// </summary>
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

    /// <summary>
    /// Creates a new game session on the specified map.
    /// </summary>
    /// <param name="map">The map on which the agent will operate.</param>
    public LocalGameSession(Tile?[,] map) // TODO: make it possible to specify starting location
    {
        this.map = map;
        isAgentAlive = true;
    }

    /// <summary>
    /// Moves the agent by the specified vector without validation.
    /// If the new location is out of bounds or contains a tile,
    /// the agent dies.
    /// </summary>
    /// <param name="delta">Vector representing the movement.</param>
    /// <returns>True if the agent remains alive after moving, false otherwise.</returns>
    internal bool Translate(Vector delta)
    {
        AgentLocation += delta;

        // Out of bounds check
        if (AgentLocation.X < 0 || AgentLocation.X >= map.GetLength(0) ||
            AgentLocation.Y < 0 || AgentLocation.Y >= map.GetLength(1))
        {
            Kill("Wandered out of the map");
        }
        // Hazard tile check
        else if (map[AgentLocation.X, AgentLocation.Y].HasValue)
        {
            DiscoveredTile = map[AgentLocation.X, AgentLocation.Y];
            Kill("Stepped on a trap");
        }

        return IsAgentAlive;
    }

    /// <summary>
    /// Attempts to move the agent using the given vector.
    /// Only valid moves are accepted. If the move is invalid,
    /// the agent does not move.
    /// </summary>
    /// <param name="move">The intended move vector.</param>
    /// <returns>
    /// A <see cref="MovementResult"/>
    /// Indicating whether the move was valid,
    /// and whether the agent is still alive afterward.
    /// </returns>
    public MovementResult Move(Vector move)
    {
        if (!IsAgentAlive)
            return new MovementResult(false, false, null);

        if (!VALID_MOVES.Contains(move))
            return new MovementResult(false, true, null);

        bool survivedMove = Translate(move);
        Tile? discovered = null;
        if (AgentLocation.X >= 0 && AgentLocation.Y >= 0 &&
           AgentLocation.X < map.GetLength(0) && AgentLocation.Y < map.GetLength(1))
            discovered = map[AgentLocation.X, AgentLocation.Y];
        return new MovementResult(true, survivedMove, discovered);
    }

    /// <summary>
    /// Kills the agent and raises the <see cref="AgentDied"/> event with the given reason.
    /// </summary>
    /// <param name="reason">Reason why the agent died.</param>
    public void Kill(string reason)
    {
        isAgentAlive = false;
        AgentDied?.Invoke(this, new AgentDiedEventArgs(reason));
    }
}
