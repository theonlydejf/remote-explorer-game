using System.Reflection;
using ExplorerGame.Core;
using ExplorerGame.Net;

static class Config
{
    [CLIHelp("Initial map width.")]
    public static uint MAP_START_WIDTH = 10;
    [CLIHelp("Initial map height.")]
    public static uint MAP_START_HEIGHT = 10;
    [CLIHelp("How much to grow the map in one dimension.")]
    public static uint MAP_GROW_SIZE_1D = 6;
    [CLIHelp("How much to grow the map in two dimensions.")]
    public static uint MAP_GROW_SIZE_2D = 3;

    [CLIHelp("Prefix used in agent's SID.")]
    public static char AGENT_SID_PREFIX = '[';
    [CLIHelp("Color used in agent's SID.")]
    public static ConsoleColor AGENT_SID_COLOR = ConsoleColor.Blue;
    [CLIHelp("Total number of agents.")]
    public static uint AGENT_CNT = 5;
    [CLIHelp("Number of the total agents should have jumping enabled.")]
    public static uint AGENT_JUMPER_CNT = 5;
    [CLIHelp("Max tries for agent to find a destination.")]
    public static uint AGENT_MAX_TRIES = 10;

    [CLIHelp("Server IP address.")]
    public static string SERVER_IP = "127.0.0.1";
    [CLIHelp("Server port.")]
    public static uint SERVER_PORT = 8080;
    [CLIHelp("Player name.")]
    public static string PLAYER_NAME = "Example";

    [CLIHelp("Display update interval in milliseconds.")]
    public static uint DISPLAY_UPDATE_INETRVAL_MS = 20;

    /// <summary>
    /// Updates config fields from CLI args in the form --field=value. Case insensitive, udnerscores
    /// are replacable by dashes
    /// </summary>
    public static void ApplyArgs(string[] args)
    {
        bool error = false;
        foreach (var arg in args)
        {
            if (!arg.StartsWith("--"))
                continue;

            var split = arg.Substring(2).Split('=', 2);
            if (split.Length != 2)
                continue;

            var name = split[0].Replace("-", "_").ToUpperInvariant();
            var value = split[1];

            var field = typeof(Config).GetFields(BindingFlags.Static | BindingFlags.Public)
                .FirstOrDefault(f => f.Name.ToUpperInvariant() == name);
            
            if (field == null)
            {
                error = true;
                Console.Write("[");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("Warning");
                Console.ResetColor();
                Console.WriteLine($"] Unknown config identifier '{name}'. Ignoring...");
                continue;
            }

            try
            {
                object converted = field.FieldType.IsEnum
                    ? Enum.Parse(field.FieldType, value, true)
                    : Convert.ChangeType(value, field.FieldType);
                field.SetValue(null, converted);
            }
            catch
            {
                error = true;
                Console.Write("[");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("Warning");
                Console.ResetColor();
                Console.WriteLine($"] Could not set config {field.Name} to {value}. Ignoring...");
            }
        }

        if (error)
        {
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
        }
    }

    public static void PrintHelp()
    {
        Console.WriteLine("Available configuration options (use --option=value):");

        foreach (var field in typeof(Config).GetFields(BindingFlags.Static | BindingFlags.Public))
        {
            var helpAttr = field.GetCustomAttribute<CLIHelpAttribute>();
            string help = helpAttr != null ? helpAttr.HelpText : "";
            Console.WriteLine($"  --{field.Name.ToLower().Replace("_", "-")}: {field.GetValue(null)} (type: {field.FieldType.Name}){(string.IsNullOrWhiteSpace(help) ? "" : " - " + help)}");
        }
    }
}

[AttributeUsage(AttributeTargets.Field)]
public class CLIHelpAttribute : Attribute
{
    public string HelpText { get; }
    public CLIHelpAttribute(string helpText) => HelpText = helpText;
}

class Program
{
    static void Main(string[] args)
    {
        if (args.Any(a => a.Equals("--help", StringComparison.OrdinalIgnoreCase) || a.Equals("-h", StringComparison.OrdinalIgnoreCase)))
        {
            Config.PrintHelp();
            return;
        }

        Config.ApplyArgs(args);

        RemoteGameSessionFactory factory = new RemoteGameSessionFactory($"http://{Config.SERVER_IP}:{Config.SERVER_PORT}/", Config.PLAYER_NAME);

        // Map with a starting size
        Map map = new Map((int)Config.MAP_START_WIDTH, (int)Config.MAP_START_HEIGHT);

        // Init agents
        Agent[] agents = new Agent[Config.AGENT_CNT];
        for (int i = 0; i < agents.Length; i++)
        {
            agents[i] = new Agent(factory, new SessionIdentifier(Config.AGENT_SID_PREFIX.ToString() + i, Config.AGENT_SID_COLOR), map, i < Config.AGENT_JUMPER_CNT);
            agents[i].Step();
        }

        DateTime lastDisplayUpdate = DateTime.UnixEpoch;
        while (true)
        {
            // Handle movement of each agent
            foreach (Agent agent in agents)
            {
                if (!agent.StepMovementResult.Ready)
                    continue;

                if (agent.ProcessFinishedStep())
                    agent.Step();
            }

            // Limit display updates
            if (DateTime.Now.Subtract(lastDisplayUpdate) < TimeSpan.FromMilliseconds(Config.DISPLAY_UPDATE_INETRVAL_MS))
                continue;
            lastDisplayUpdate = DateTime.Now;

            // Display the current map
            Console.Clear();
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    // If agent is here
                    if (agents.Any(agent => !agent.NowhereToGo && agent.Location == new Vector(x, y)))
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write("<>");
                        continue;
                    }

                    // Print the cell
                    var (symbol, color) = CellStateInfo.GetDisplay(map[x, y]);
                    Console.ForegroundColor = color;
                    Console.Write(symbol);
                }
                Console.WriteLine();
            }

            // If all agents cannot find new destinations
            if (agents.All(agent => agent.NowhereToGo))
                break;
        }

    }
}

enum CellState
{
    Undiscovered,
    Safe,
    Trap,
    Unreachable,
    Reserved
}

static class CellStateInfo
{
    public static (string Symbol, ConsoleColor Color) GetDisplay(CellState state)
    {
        return state switch
        {
            CellState.Undiscovered => ("**", ConsoleColor.DarkGray),
            CellState.Safe         => ("..", ConsoleColor.White),
            CellState.Trap         => ("XX", ConsoleColor.Red),
            CellState.Unreachable  => ("??", ConsoleColor.Yellow),
            CellState.Reserved     => ("**", ConsoleColor.Magenta),
            _                      => ("??", ConsoleColor.DarkRed)
        };
    }
}

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
    public SessionIdentifier Identifier { get; }
    
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
            allowJumps = value;
            if (value)
                JumpingAgentsCnt++;
            else
                JumpingAgentsCnt--;
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
    public Agent(RemoteGameSessionFactory factory, SessionIdentifier identifier, Map map, bool allowJumps = true)
    {
        this.factory = factory;
        Identifier = identifier;
        Map = map;
        AllowJumps = allowJumps;
        StepMovementResult = new AsyncMovementResult(true, new MovementResult(false, true), Task.CompletedTask);

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
        Vector? vec = Map.PickClosestUndiscovered(Location);
        while (vec == null)
        {
            if (GrowMap((int)Config.MAP_GROW_SIZE_1D, (int)Config.MAP_GROW_SIZE_2D))
            {
                NowhereToGo = true;
                return new Vector(0, 0);
            }
            vec = Map.PickClosestUndiscovered(Location);
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

            foreach (var neighbor in GetNeighbors(current))
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

    IEnumerable<Vector> GetNeighbors(Vector pos)
    {
        int[] dx, dy;
        if (AllowJumps)
        {
            dx = new int[] { 1, -1, 0, 0, 2, -2, 0, 0 };
            dy = new int[] { 0, 0, 1, -1, 0, 0, 2, -2 };
        }
        else
        {
            dx = new int[] { 1, -1, 0, 0 };
            dy = new int[] { 0, 0, 1, -1 };
        }

        for (int i = 0; i < dx.Length; i++)
        {
            int nx = pos.X + dx[i];
            int ny = pos.Y + dy[i];

            if (nx < 0 || ny < 0 || nx >= Map.Width || ny >= Map.Height)
                continue;

            // Only allow certain cells
            if (new CellState[] { CellState.Safe, CellState.Undiscovered, CellState.Reserved }.Contains(Map[nx, ny]))
                yield return new Vector(nx, ny);
        }
    }

    Vector ReconstructFirstStep(Dictionary<Vector, Vector> cameFrom, Vector current, Vector start)
    {
        Vector prev = current;
        while (cameFrom.TryGetValue(prev, out Vector parent) && !parent.Equals(start))
            prev = parent;
        return prev;
    }
}

/// <summary>
/// Represents a 2D grid of cells for the game world, tracking each cell’s state (undiscovered,
/// safe, trap, unreachable, or reserved). Supports dynamic resizing, cell state updates,
/// and efficient searching for undiscovered cells. Provides methods to grow the map,access
/// cells, and select the closest or a random undiscovered cell for exploration.
/// </summary>
class Map
{
    private CellState[,] map;

    
    public Map(int width, int height)
    {
        map = new CellState[width, height];
        UndiscoveredCnt = width * height;
    }

    public int Width => map.GetLength(0);
    public int Height => map.GetLength(1);

    /// <summary>
    /// How many undiscovered cells are in the map
    /// </summary>
    public int UndiscoveredCnt { get; private set; }

    public CellState this[Vector vec]
    {
        get => this[vec.X, vec.Y];
        set => this[vec.X, vec.Y] = value;
    }

    public CellState this[int row, int col]
    {
        get
        {
            if (row < 0 || col < 0)
                return CellState.Trap; // Too far left -> always deadly

            if (row >= Width || col >= Height)
                return CellState.Undiscovered; // Too far right -> map can grow this way, so maybe something?

            return map[row, col];
        }
        set
        {
            if (row < 0 || col < 0 || row >= Width || col >= Height)
                return; // ignore out of bounds write

            if (map[row, col] == CellState.Undiscovered && value != CellState.Undiscovered)
                UndiscoveredCnt--;
            if (map[row, col] != CellState.Undiscovered && value == CellState.Undiscovered)
                UndiscoveredCnt++;

            map[row, col] = value;
        }
    }

    /// <summary>
    /// Expands the map by increasing its width and/or height. Newly added cells are set to Undiscovered.
    /// Previously Unreachable cells are reset to Undiscovered, allowing further exploration.
    /// Updates the count of undiscovered cells.
    /// </summary>
    /// <param name="widthIncrease">Columns to add</param>
    /// <param name="heightIncrease">Rows to add</param>
    public void Grow(int widthIncrease = 1, int heightIncrease = 1)
    {
        int newWidth = Width + widthIncrease;
        int newHeight = Height + heightIncrease;
        var newMap = new CellState[newWidth, newHeight];

        // Copy old map contents to new map
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (map[x, y] == CellState.Unreachable) // Maybe a new route to unreachable cells?
                {
                    newMap[x, y] = CellState.Undiscovered;
                    UndiscoveredCnt++;
                }
                else
                    newMap[x, y] = map[x, y];
            }
        }

        // Count new undiscovered cells
        UndiscoveredCnt += (newWidth * newHeight) - (Width * Height);

        map = newMap;
    }

    /// <summary>
    /// Finds the undiscovered cell closest to the given point using BFS
    /// </summary>
    /// <param name="point">The starting position for the search</param>
    /// <returns>The closest undiscovered cell, or null if all cells are discovered.</returns>
    public Vector? PickClosestUndiscovered(Vector point)
    {
        var visited = new bool[Width, Height];
        var queue = new Queue<Vector>();
        queue.Enqueue(point);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            int x = current.X, y = current.Y;

            if (x < 0 || y < 0 || x >= Width || y >= Height || visited[x, y])
                continue;

            visited[x, y] = true;

            if (map[x, y] == CellState.Undiscovered)
                return current;

            // Add neighbors (up, down, left, right)
            queue.Enqueue(new Vector(x + 1, y));
            queue.Enqueue(new Vector(x - 1, y));
            queue.Enqueue(new Vector(x, y + 1));
            queue.Enqueue(new Vector(x, y - 1));
        }
        return null;
    }

    /// <summary>
    /// Picks a random undiscovered cell in the map using the provided random number generator.
    /// Returns null if there are no undiscovered cells.
    /// </summary>
    /// <param name="rng">Random number generator to use for selection.</param>
    /// <returns>A random undiscovered cell, or null if none exist</returns>
    public Vector? PickRandomUndiscovered(Random rng)
    {
        if (UndiscoveredCnt <= 0)
            return null;

        int target = rng.Next(UndiscoveredCnt);
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (map[x, y] == CellState.Undiscovered)
                {
                    if (target == 0)
                        return new Vector(x, y);
                    target--;
                }
            }
        }
        return null;
    }
}