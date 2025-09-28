using ExplorerGame.Core;
using ExplorerGame.Net;

class Agent
{
    private static object Sync = new object();
    private static int JumpingAgentsCnt = 0;

    /// <summary>
    /// Current targetted destination of the agent to wich he is travelling
    /// </summary>
    public Vector Destination { get; set; }

    /// <summary>
    /// Current position of the agent
    /// </summary>
    public Vector Location { get; set; }

    /// <summary>
    /// Assigned SID of this agent
    /// </summary>
    public SessionIdentifier? Identifier { get; }
    
    /// <summary>
    /// Reference to a Map, which the agent is trying to discover
    /// </summary>
    public Map Map { get; }

    /// <summary>
    /// AsyncMovementResult of the movement performed by last Step()
    /// </summary>
    public AsyncMovementResult StepMovementResult { get; private set; }

    /// <summary>
    /// True, if the agent should be allowed to jump
    /// </summary>
    public bool AllowJumps
    {
        get => allowJumps;
        set
        {
            if (allowJumps != value)
            {
                if (value)
                    JumpingAgentsCnt++;
                else
                    JumpingAgentsCnt--;
            }
            allowJumps = value;
        }
    }
    private bool allowJumps;

    /// <summary>
    /// True, when this agent has nowhere to go and there is a big chance that all the others
    /// won't have either
    /// </summary>
    public bool NowhereToGo { get; private set; } = false;

    // GameSession connection to the current live agent
    private RemoteGameSession gameSession;
    private RemoteGameSessionFactory factory;
    // The current step that is in progress
    private Vector? currStep;
    // Location of the cell that this agenthas reserved. null if none reserved
    private Vector? reservedCellLoc;

    /// <summary>
    /// Creates instance of Agent and initiates new game session with the remote agent
    /// </summary>
    /// <param name="factory"></param>
    /// <param name="identifier"></param>
    /// <param name="map"></param>
    /// <param name="allowJumps"></param>
    public Agent(RemoteGameSessionFactory factory, SessionIdentifier? identifier, Map map, bool allowJumps = true)
    {
        this.factory = factory;
        Identifier = identifier;
        Map = map;
        AllowJumps = allowJumps;
        StepMovementResult = new AsyncMovementResult(true, new MovementResult(false, true, null), Task.CompletedTask);

        lock (Sync)
        {
            newDestination();
        }
        gameSession = factory.Create(identifier);
    }

    /// <summary>
    /// Steps in the direction of the current destionation. If the current dest is not reachable
    /// or the agent has reached it, a new destination is chosen and the step is taken towards
    /// the new valid destination.
    /// </summary>
    public void Step()
    {
        // If the target cell has already been discovered or agent didn't move => find new dest
        if (!new[] { CellState.Undiscovered, CellState.Reserved }.Contains(Map[Destination])
            || currStep == new Vector(0, 0))
            newDestination();

        currStep = NextStep(); // Find the next step along the shortest path to destination

        if (currStep == new Vector(0, 0)) // If at the destination => find new dest
        {
            lock (Sync)
            {
                newDestination();
            }
            currStep = NextStep();
        }

        if (!AllowJumps && JumpingAgentsCnt > 0)
        {
            // Non-jumper found unreachable destination => try different destinations
            /*  Limit tries v------------------------v    v--------------v check if valid dest was found */
            for (int i = 0; i < Config.AGENT_MAX_TRIES && currStep == null; i++)
            {
                newRandomDestination();
                currStep = NextStep();
            }

            // If no reachable destinations found => wait
            if (currStep == null)
            {
                currStep = new Vector(0, 0);
                releaseReservedCell();
            }
        }
        else while (currStep == null)
        {
            // Jumper found unreachable destination or no jumping agents exist
            lock (Sync)
            {
                Map[Destination] = CellState.Unreachable;
                newDestination();
            }
            currStep = NextStep();
        }

        // Start moving the agent
        StepMovementResult = gameSession.MoveAsync(currStep.Value);
    }

    private void newDestination()
    {
        releaseReservedCell();
        Destination = getClosestPos();
        reserveCell(Destination);
    }

    private void newRandomDestination()
    {
        Destination = getRndPos();
        reserveCell(Destination);
    }

    private void releaseReservedCell()
    {
        if (!reservedCellLoc.HasValue)
            return;

        if (Map[reservedCellLoc.Value] == CellState.Reserved)
            Map[reservedCellLoc.Value] = CellState.Undiscovered;
        return;
    }

    private void reserveCell(Vector location)
    {
        if (reservedCellLoc.HasValue)
            releaseReservedCell();
        
        reservedCellLoc = location;
        Map[reservedCellLoc.Value] = CellState.Reserved;
    }

    /// <summary>
    /// Checks the result of the last steps movement and updates the map and the agent accordingly
    /// </summary>
    /// <returns>True if next step should be taken</returns>
    public bool ProcessFinishedStep()
    {
        if (NowhereToGo)
            return false;
        
        if (currStep == null)
            return true;
        
        // Update location
        Location += currStep.Value;

        lock (Sync)
        {
            // Movement did not happen
            if (StepMovementResult.MovementResult?.MovedSuccessfully == false)
                return true;

            // Agent survived the movement
            if (StepMovementResult.MovementResult?.IsAgentAlive == true) // TODO not update map every time?
            {
                Map[Location] = CellState.Safe;
                return true;
            }

            // Agent died
            gameSession = factory.Create(Identifier);
            Map[Location] = CellState.Trap;
            Location = new(0, 0);
            newDestination();
        }
        return true;
    }

    /// <summary>
    /// Gets closest undiscovered location. If none are found, grows the map
    /// </summary>
    Vector getClosestPos()
    {
        Vector? vec = Map.PickClosestUndiscovered(Location, allowJumps);
        while (vec == null)
        {
            if (GrowMap((int)Config.MAP_GROW_SIZE_1D, (int)Config.MAP_GROW_SIZE_2D))
            {
                NowhereToGo = true;
                return new Vector(0, 0);
            }
            vec = Map.PickClosestUndiscovered(Location, allowJumps);
        }
        return vec.Value;
    }
    
    /// <summary>
    /// Gets random undiscovered location. If none are found, grows the map
    /// </summary>
    Vector getRndPos()
    {
        Vector? vec = Map.PickRandomUndiscovered(Random.Shared);
        while (vec == null)
        {
            if (GrowMap((int)Config.MAP_GROW_SIZE_1D, (int)Config.MAP_GROW_SIZE_2D))
            {
                NowhereToGo = true;
                return new Vector(0, 0);
            }
            vec = Map.PickRandomUndiscovered(Random.Shared);
        }
        return vec.Value;
    }

    /// <summary>
    /// Grows map in a direction which has better chance of discovering new safe cells.
    /// </summary>
    /// <param name="growSize1D">How much to grow if growing in one dimension</param>
    /// <param name="growSize2D">How much to grow if growing in two dimensions</param>
    /// <returns>
    /// True hen both the right and bottom edges of the map are completely filled with 
    /// unreachable or trap cells
    /// </returns>
    private bool GrowMap(int growSize1D = 2, int growSize2D = 1)
    {
        // Count bad cells on bottom and right edges
        int layerCheckCnt = JumpingAgentsCnt > 0 ? 2 : 1; // How many layers check
        int rightScore = 0, bottomScore = 0;
        CellState[] badCellStates = { CellState.Unreachable, CellState.Trap };

        // Right edge
        for (int dx = 0; dx < layerCheckCnt; dx++)
        {
            for (int y = 0; y < Map.Height; y++)
            {
                if (badCellStates.Contains(Map[Map.Width - 1 - dx, y]))
                    rightScore++;
            }
        }
        // Bottom edge
        for (int dy = 0; dy < layerCheckCnt; dy++)
        {
            for (int x = 0; x < Map.Width; x++)
            {
                if (badCellStates.Contains(Map[x, Map.Height - 1 - dy]))
                    bottomScore++;
            }
        }

        // Both edges blocked
        if (rightScore >= layerCheckCnt * Map.Height && bottomScore >= layerCheckCnt * Map.Width)
            return true;

        if (rightScore >= layerCheckCnt * Map.Height) // Only right edge blocked
            Map.Grow(0, growSize1D);
        else if (bottomScore >= layerCheckCnt * Map.Width) // Only bottom edge blocked
            Map.Grow(growSize1D, 0);
        else if (bottomScore > rightScore) // Grow in the direction with more blocked cells, or diagonally if equal
            Map.Grow(growSize1D, 0);
        else if (rightScore > bottomScore)
            Map.Grow(0, growSize1D);
        else
            Map.Grow(growSize2D, growSize2D);

        return false;
    }

    Vector? NextStep()
    {
        // A* algorithm
        var openSet = new SortedSet<(float fScore, int tieBreaker, Vector pos)>(Comparer<(float, int, Vector)>.Create((a, b) =>
        {
            int cmp = a.Item1.CompareTo(b.Item1);
            if (cmp != 0) return cmp;
            cmp = a.Item2.CompareTo(b.Item2);
            if (cmp != 0) return cmp;
            // Compare by X then Y for deterministic ordering
            cmp = a.Item3.X.CompareTo(b.Item3.X);
            if (cmp != 0) return cmp;
            return a.Item3.Y.CompareTo(b.Item3.Y);
        }));

        var cameFrom = new Dictionary<Vector, Vector>();
        var gScore = new Dictionary<Vector, float> { [Location] = 0 };
        var fScore = new Dictionary<Vector, float> { [Location] = Heuristic(Location, Destination) };

        int tieBreaker = 0;
        openSet.Add((fScore[Location], tieBreaker++, Location));

        while (openSet.Count > 0)
        {
            var current = openSet.Min.pos;
            if (current.Equals(Destination))
                return ReconstructFirstStep(cameFrom, current, Location) - Location;

            openSet.Remove(openSet.Min);

            foreach (var neighbor in NeighborUtils.GetNeighbors(Map, current, AllowJumps))
            {
                float tentativeG = gScore[current] + 1;
                if (gScore.TryGetValue(neighbor, out float g) && tentativeG >= g)
                    continue;

                cameFrom[neighbor] = current;
                gScore[neighbor] = tentativeG;
                fScore[neighbor] = tentativeG + Heuristic(neighbor, Destination);
                openSet.Add((fScore[neighbor], tieBreaker++, neighbor));
            }
        }

        // No path found
        return null;
    }

    float Heuristic(Vector a, Vector b)
    {
        // Manhattan distance
        return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    }

    Vector ReconstructFirstStep(Dictionary<Vector, Vector> cameFrom, Vector current, Vector start)
    {
        Vector prev = current;
        while (cameFrom.TryGetValue(prev, out Vector parent) && !parent.Equals(start))
            prev = parent;
        return prev;
    }
}
