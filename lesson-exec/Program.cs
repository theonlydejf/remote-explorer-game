using ExplorerGame.Base;
using ExplorerGame.Core;
using ExplorerGame.ConsoleVisualizer;
using ExplorerGame.Net;
using System.Text.RegularExpressions;

public static class ConsoleSync
{
    public static object sync = new object();
}

class WorldInfo
{
    public string Name { get; set; }
    public ConsoleColor Color { get; set; }
    public int Port { get; set; }
    public Tile?[,] Map { get; set; }
    public ConnectionHandler? connectionHandler = null;

    public WorldInfo(string name, ConsoleColor color, int port, Tile?[,] map)
    {
        Name = name;
        Color = color;
        Port = port;
        Map = map;
    }
}

public partial class Program
{
    private enum VisualizerHAlignement
    {
        Left,
        Centered,
        Right
    }

    public static void Main(string[] args)
    {
        // Test world map
        Tile?[,] testWorldMap = GameFactory.MapFromImage(Path.Combine(AppContext.BaseDirectory, "resources", "test-map.png"));
        WorldInfo testWorldInfo = new WorldInfo(
                $"Test World",
                ConsoleColor.Cyan,
                8080,
                testWorldMap
        );

        // Load challange world maps
        string resourcesPath = "resources";
        string challengesDir = Path.Combine(resourcesPath, "challenges");
        string[] challengeFiles = Array.Empty<string>();
        if (Directory.Exists(challengesDir))
        {
            challengeFiles = Directory.GetFiles(challengesDir, "challenge-*.png")
                .Where(f => Regex.IsMatch(Path.GetFileName(f), @"^challenge-\d+\.png$"))
                .OrderBy(f => int.Parse(Regex.Match(Path.GetFileName(f), @"\d+").Value))
                .ToArray();
        }

        var worlds = challengeFiles
            .Select((f, i) => new WorldInfo(
                $"Challenge {i + 1}",
                ConsoleColor.Green,
                8081 + i,
                GameFactory.MapFromImage(f)
            )).ToList();
        worlds.Insert(0, testWorldInfo);

        Logger logger;
        bool winTooSmall;
        while (true)
        {
            winTooSmall = Console.WindowWidth < testWorldMap.GetLength(0) * 2 + 2;

            // Initial terminal setup
            Console.CursorVisible = false;
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            string exitMsg = "Press ESC or Q to exit";
            Console.SetCursorPosition(Console.WindowWidth / 2 - exitMsg.Length / 2, Console.WindowHeight - 1);
            Console.Write(exitMsg);
            Console.ResetColor();

            try
            {
                logger = new Logger(1, testWorldMap.GetLength(1) + 3, Console.WindowWidth - 3, Console.WindowHeight - testWorldMap.GetLength(1) - 4, ConsoleColor.White, Console.BackgroundColor, ConsoleSync.sync);
                if (!winTooSmall)
                    break;
            }
            catch
            {
                winTooSmall = true;
            }

            if (winTooSmall)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                string message = "Terminal window is too small\nResize it and press any key to try again...";
                var lines = message.Split('\n');
                int top = Console.WindowHeight / 2 - lines.Length / 2;
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    int left = (Console.WindowWidth - line.Length) / 2;
                    Console.SetCursorPosition(left > 0 ? left : 0, top + i > 0 ? top + i : 0);
                    Console.Write(line);
                }
                Console.CursorVisible = true;
                if (IsExitKey(Console.ReadKey(true).Key))
                {
                    CleanUp();
                    Environment.Exit(1);
                    return;
                }
            }
        }

        ConsoleVisualizer viz = new ConsoleVisualizer(new(Console.WindowWidth / 2 - (testWorldMap.GetLength(0) * 2 + 2) / 2, 0), ConsoleSync.sync);
        viz.AttachMap(testWorldMap);

        // Start test world server
        CancellationTokenSource cts = new();
        testWorldInfo.connectionHandler = new(testWorldMap, viz);
        Task testServerTask = testWorldInfo.connectionHandler.StartHttpServer(testWorldInfo.Port, cts.Token);
        testWorldInfo.connectionHandler.SessionConnected += new SessionConnectedLogger(
            logger,
            testWorldInfo,
            () => UpdateServerStatus()
        ).Handler;
        testWorldInfo.connectionHandler.SessionConnected += (sender, e) => UpdateServerStatus();
        logger.Write(testWorldInfo.Name, testWorldInfo.Color);
        logger.WriteLine($" server started on port {testWorldInfo.Port}", ConsoleColor.Yellow);

        // Start challenge world servers
        Task[] challengeServerTasks = new Task[worlds.Count];
        for (int i = 1; i < worlds.Count; i++)
        {
            worlds[i].connectionHandler = new ConnectionHandler(worlds[i].Map, null);
            worlds[i].connectionHandler!.SessionConnected += new SessionConnectedLogger(
                logger,
                worlds[i],
                () => UpdateServerStatus()
            ).Handler;
            worlds[i].connectionHandler!.SessionConnected += (sender, e) => UpdateServerStatus();
            challengeServerTasks[i] = worlds[i].connectionHandler!.StartHttpServer(worlds[i].Port, cts.Token);
            logger.Write(worlds[i].Name, worlds[i].Color);
            logger.WriteLine($" server started on port {worlds[i].Port}", ConsoleColor.Yellow);
        }

        VisualizerHAlignement vizAlignement = UpdateServerStatus();
        if (vizAlignement == VisualizerHAlignement.Left)
            viz.WindowLocation = new(1, 0);
        else if (vizAlignement == VisualizerHAlignement.Right)
            viz.WindowLocation = new(Console.WindowWidth - testWorldMap.GetLength(0)*2 - 2 - 2, 0);
        UpdateServerStatus();

        // Wait until exit key is pressed
        while (!IsExitKey(Console.ReadKey(true).Key)) { /* Pass */ }

        // Clear up the terminal
        CleanUp();
        cts.Cancel();

        VisualizerHAlignement UpdateServerStatus()
        {
            // Line format
            // <world name> on <port>: XX agents
            //  port - 5 digits at most are expected
            //  clients - 3 digits at most are expected
            // => roughly worldName.Len + 22 max msg len expected
            const int MAX_EXPECTED_POSTFIX_LEN = 22;

            VisualizerHAlignement vizLoc;
            int maxLen = 0;
            foreach (var world in worlds)
            {
                int len = world.Name.Length + MAX_EXPECTED_POSTFIX_LEN;
                if (len > maxLen)
                    maxLen = len;
            }

            if (maxLen >= Console.WindowWidth - testWorldMap.GetLength(0) * 2 - 3)
                return VisualizerHAlignement.Centered;
            else if (maxLen >= Console.WindowWidth / 2 - (testWorldMap.GetLength(0) * 2 + 2) / 2 + 1)
                vizLoc = VisualizerHAlignement.Right;
            else
                vizLoc = VisualizerHAlignement.Centered;

            Console.SetCursorPosition(0, (testWorldMap.GetLength(1) + 1) / 2 - worlds.Count / 2);
            foreach (var world in worlds)
            {
                Console.Write(' ');
                Console.ForegroundColor = world.Color;
                Console.Write(world.Name);
                Console.ResetColor();
                Console.Write(new string(' ', maxLen - MAX_EXPECTED_POSTFIX_LEN - world.Name.Length));
                Console.WriteLine(
                    $" on {world.Port}: {(world.connectionHandler?.SessionCount)?.ToString() ?? "ERR"} agents"
                    .PadRight(MAX_EXPECTED_POSTFIX_LEN)
                );
            }

            return vizLoc;
        }
    }

    static bool IsExitKey(ConsoleKey key) => new[] { ConsoleKey.Escape, ConsoleKey.Q }.Contains(key);

    static void CleanUp()
    {
        Console.CursorVisible = true;
        Console.Clear();
    }
}

class SessionConnectedLogger
{
    private Logger logger;
    private string world;
    private ConsoleColor worldColor;
    private Action? updateAgentStats;

    public SessionConnectedLogger(Logger logger, WorldInfo worldInfo, Action? updateAgentStats = null)
    {
        this.logger = logger;
        world = worldInfo.Name;
        worldColor = worldInfo.Color;
        this.updateAgentStats = updateAgentStats;
    }

    public void Handler(object? sender, SessionConnectedEventArgs e)
    {
        lock (ConsoleSync.sync)
        {
            logger.Write("[", ConsoleColor.White);
            logger.Write(e.ClientUsername, ConsoleColor.Yellow);
            logger.Write(" @" + e.ClientID, ConsoleColor.DarkGray);
            logger.Write("] ", ConsoleColor.White);
            if (!e.Response.Value<bool>("success"))
            {
                logger.Write("Agent connection failed (", ConsoleColor.Red);
                logger.Write(world, worldColor);
                logger.WriteLine(")", ConsoleColor.Red);
                return;
            }

            
            logger.Write("Connected ", ConsoleColor.White);
            if (e.SessionIdentifier != null && e.SessionIdentifier.HasVSID)
            {
                logger.Write("with '", ConsoleColor.White);
                logger.Write(e.SessionIdentifier.IdentifierStr, e.SessionIdentifier.Color.Value);
                logger.Write("' ", ConsoleColor.White);
            }
            logger.Write("into ", ConsoleColor.White);
            logger.WriteLine(world, worldColor);

            if (e.GameSession == null)
            {
                logger.WriteLine("Connection without session detected!", ConsoleColor.White, ConsoleColor.Red);
                return;
            }
        }

        e.GameSession.AgentDied += new AgentDiedLogger(e, logger, world, worldColor).Handler;
        e.GameSession.AgentDied += (sender, e) => updateAgentStats?.Invoke();
    }
}

class AgentDiedLogger
{
    private SessionConnectedEventArgs sessionArgs;
    private Logger logger;
    private string world;
    private ConsoleColor worldColor;

    public AgentDiedLogger(SessionConnectedEventArgs sessionArgs, Logger logger, string world, ConsoleColor worldColor)
    {
        this.sessionArgs = sessionArgs;
        this.logger = logger;
        this.world = world;
        this.worldColor = worldColor;
    }

    public void Handler(object? sender, AgentDiedEventArgs e)
    {
        lock (ConsoleSync.sync)
        {
            logger.Write("[", ConsoleColor.White);
            logger.Write(sessionArgs.ClientUsername, ConsoleColor.Yellow);
            logger.Write(" @" + sessionArgs.ClientID, ConsoleColor.DarkGray);
            logger.Write("] Died in '", ConsoleColor.White);
            logger.Write(world, worldColor);
            logger.WriteLine($"' (reason: '{e.DeathReason}')", ConsoleColor.White);
        }
    }
}