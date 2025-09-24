/*
    Example Solution - Remote Explorer Game

    This program simulates multiple agents exploring a 2D grid-based map by communicating with a remote game server.
    Each agent operates in its own session, discovers new cells, and updates a shared map representation.

    Key Components:
    ----------------
    - Config: Holds all configurable parameters (map size, agent count, server address, etc.), which can be overridden via CLI.
    - Agent: Represents an autonomous explorer. Each agent:
        * Maintains its own session with the server.
        * Selects a destination (closest or random undiscovered cell).
        * Uses A* pathfinding to move toward the destination.
        * Marks cells as Safe, Trap, Unreachable, or Reserved based on exploration results.
        * Can be configured to "jump" (move in larger steps) or not.
        * If an agent dies, it is respawned at the starting position.
        * If no more undiscovered cells are reachable, the agent can trigger map growth.
    - Map: Represents the shared 2D grid of cell states. Supports:
        * Dynamic resizing (growth) when agents reach the map edge.
        * Efficient search for closest or random undiscovered cells.
        * Tracking of how many undiscovered cells remain.
    - Main Loop:
        * Initializes the map and agents.
        * Each iteration, processes agent movement results and triggers new steps.
        * Periodically redraws the map in the console, showing agent positions and cell states.
        * Terminates when all agents have nowhere left to explore.

    CLI Usage:
    ----------
    - All config parameters can be overridden via command-line arguments, e.g.:
        dotnet run -- --map-start-width=20 --agent-cnt=10 --server-ip=192.168.1.5
    - Use --help to list all available configuration options.

    Cell States:
    ------------
    - Undiscovered: Not yet visited by any agent.
    - Safe: Explored and traversable.
    - Trap: Deadly cell; agent dies if it enters.
    - Unreachable: Determined to be impossible to reach (e.g., surrounded by traps).
    - Reserved: Temporarily reserved by an agent as its next destination.


    DETAILED PROGRAM WALKTHROUGH

    This program is a complete example of how to solve the "Remote Explorer Game" task using multiple autonomous agents
    that explore a dynamically growing 2D map by communicating with a remote server. Below is a step-by-step explanation
    of how the program works, highlighting the most important concepts and code sections.

    1. CONFIGURATION AND CLI
    ------------------------
    - The `Config` static class holds all configurable parameters (map size, agent count, server address, etc.).
    - Each parameter is annotated with `[CLIHelp("...")]` for automatic help generation.
    - The `ApplyArgs` method parses command-line arguments (e.g., `--map-start-width=20`) and updates the config fields.
    - The `PrintHelp` method lists all available config options and their descriptions.
    - This allows easy experimentation and tuning without code changes.

    2. MAIN PROGRAM FLOW
    --------------------
    - The `Main` method first checks for `--help` and prints available options if requested.
    - It then applies any CLI overrides to the config.
    - A `RemoteGameSessionFactory` is created to manage connections to the remote server.
    - The shared `Map` is initialized with the configured starting size.
    - An array of `Agent` objects is created. Each agent:
        * Gets a unique session identifier and color.
        * Is assigned a reference to the shared map.
        * Is told whether it can "jump" (move in larger steps).
        * Immediately chooses its first destination and starts moving.

    3. AGENT LOGIC
    --------------
    - Each `Agent` represents an explorer with its own session on the server.
    - The agent's main method is `Step()`, which:
        * Checks if the current destination is still valid (undiscovered or reserved).
        * If not, picks a new destination (either closest or random undiscovered cell).
        * Uses A* pathfinding (`NextStep()`) to find the next move toward the destination.
        * If the agent cannot reach the destination:
            - If it cannot jump, it tries up to `AGENT_MAX_TRIES` random destinations.
            - If it can jump (or no jumpers exist), it marks the destination as unreachable and picks a new one.
        * The agent then sends a move command to the server asynchronously.
    - The agent's `ProcessFinishedStep()` method:
        * Checks the result of the last move.
        * Updates its position and the map (marking cells as Safe, Trap, etc.).
        * If the agent died, it is respawned at the starting position and a new session is created.

    4. MAP MANAGEMENT
    -----------------
    - The `Map` class represents the shared 2D grid of cells.
    - Each cell can be Undiscovered, Safe, Trap, Unreachable, or Reserved.
    - The map supports dynamic growth: when agents reach the edge and cannot find new destinations, the map grows in the direction with the most blocked cells.
    - The map tracks the number of undiscovered cells for efficient searching.
    - Agents use `PickClosestUndiscovered` (BFS) and `PickRandomUndiscovered` to select new destinations.

    5. MAIN LOOP
    ------------
    - The main loop repeatedly:
        * Checks if each agent's move has finished.
        * If so, processes the result and triggers the next step.
        * Periodically redraws the map in the console, showing all agents and cell states.
        * Exits when all agents have nowhere left to go (i.e., the map is fully explored or blocked).

    6. PATHFINDING
    --------------
    - Agents use the A* algorithm to find the shortest path to their destination.
    - The heuristic is Manhattan distance.
    - Agents with "jump" enabled can consider moves of length 2 in any direction, allowing them to bypass obstacles.

    7. MAP GROWTH STRATEGY
    ----------------------
    - When no undiscovered cells are reachable, the agent calls `GrowMap`.
    - The map grows in the direction (right or bottom) with the most blocked cells, or diagonally if both are equally blocked.
    - If both the right and bottom edges are fully blocked, the agent sets its `NowhereToGo` flag.

    8. DISPLAY
    ----------
    - The map is printed to the console, with different symbols and colors for each cell state and for agents.
    - The display is updated at a configurable interval to avoid flickering.

    9. TERMINATION
    --------------
    - The program ends when all agents have set their `NowhereToGo` flag, meaning the map is fully explored or blocked.

    10. EXTENSIBILITY
    -----------------
    - The code is modular and well-documented, making it easy to extend (e.g., add new agent behaviors, cell types, or visualization).
    - The CLI configuration system allows for easy experimentation with different parameters.

    This example demonstrates best practices for concurrent agent-based exploration, dynamic map management, robust CLI configuration, and clear code structure. It is intended as a reference solution for students tackling the Remote Explorer Game task.

*/

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
    [CLIConfigCheckBounds(1, double.PositiveInfinity)]
    public static uint MAP_GROW_SIZE_1D = 6;
    [CLIHelp("How much to grow the map in two dimensions.")]
    [CLIConfigCheckBounds(1, double.PositiveInfinity)]
    public static uint MAP_GROW_SIZE_2D = 3;

    [CLIHelp("Should the agents use VSIDs?")]
    public static bool AGENT_USE_VSID = true;
    [CLIHelp("Prefix used in agent's VSID.")]
    public static char AGENT_VSID_PREFIX = '[';
    [CLIHelp("Color used in agent's VSID.")]
    public static ConsoleColor AGENT_VSID_COLOR = ConsoleColor.Blue;
    [CLIHelp("Total number of agents.")]
    public static uint AGENT_CNT = 5;
    [CLIHelp("Number of the total agents should have jumping enabled.")]
    public static uint AGENT_JUMPER_CNT = 5;
    [CLIHelp("Max tries for agent to find a destination.")]
    [CLIConfigCheckBounds(1, double.PositiveInfinity)]
    public static uint AGENT_MAX_TRIES = 10;

    [CLIHelp("Server IP address.")]
    public static string SERVER_IP = "127.0.0.1";
    [CLIHelp("Server port.")]
    public static uint SERVER_PORT = 8080;
    [CLIHelp("Player name.")]
    public static string PLAYER_NAME = "Example";

    [CLIHelp("Display update interval in milliseconds.")]
    [CLIConfigCheckBounds(1, double.PositiveInfinity)]
    public static uint DISPLAY_UPDATE_INTERVAL_MS = 20;

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
                
                var attr = field.GetCustomAttribute<CLIConfigCheckBounds>();
                if (attr != null && !attr.IsInBounds(converted))
                {
                    error = true;
                    Console.Write("[");
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write("Warning");
                    Console.ResetColor();
                    Console.WriteLine($"] Value {value} is not in bounds for {field.Name} (min={attr.Min}, max={attr.Max}). Ignoring...");
                    continue;
                }

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

    public CLIHelpAttribute(string helpText)
    {
        HelpText = helpText;
    }
}

[AttributeUsage(AttributeTargets.Field)]
public class CLIConfigCheckBounds : Attribute
{
    // Use double.MinValue and double.MaxValue as sentinels for "no bound"
    public double Min { get; }
    public double Max { get; }

    public CLIConfigCheckBounds(double min, double max)
    {
        Min = min;
        Max = max;
    }

    /// <summary>
    /// Checks if the given value is within the bounds specified by this attribute.
    /// Supports numeric types (int, uint, float, double, long, etc.).
    /// </summary>
    public bool IsInBounds(object value)
    {
        if (value == null)
            return false;

        try
        {
            double d = Convert.ToDouble(value);
            return (!double.IsRealNumber(Min) || d >= Min) && (!double.IsRealNumber(Max) || d <= Max);
        }
        catch
        {
            return false;
        }
    }
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
            SessionIdentifier? sid = null;
            if (Config.AGENT_USE_VSID)
            {
                // Supports up to 64 agents
                /* 🐷 */ string postfix = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz?!"[i % 64].ToString();
                sid = new SessionIdentifier(Config.AGENT_VSID_PREFIX.ToString() + postfix, Config.AGENT_VSID_COLOR);
            }
            agents[i] = new Agent(factory, sid, map, i < Config.AGENT_JUMPER_CNT);
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
            if (DateTime.Now.Subtract(lastDisplayUpdate) < TimeSpan.FromMilliseconds(Config.DISPLAY_UPDATE_INTERVAL_MS))
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