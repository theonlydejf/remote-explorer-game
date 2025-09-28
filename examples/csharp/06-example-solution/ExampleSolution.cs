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

using ExplorerGame.Core;
using ExplorerGame.Net;

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
