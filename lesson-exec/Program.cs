using ExplorerGame.Base;
using ExplorerGame.Core;
using ExplorerGame.ConsoleVisualizer;
using ExplorerGame.Net;
using System.Text.RegularExpressions;

public static class ConsoleSync
{
    public static object sync = new object();
}

public partial class Program
{
    public static void Main(string[] args)
    {
        // Test world map
        Tile?[,] testWorldMap = GameFactory.MapFromImage(Path.Combine(AppContext.BaseDirectory, "resources", "test-map.png"));

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
        Tile?[][,] challangeMaps = challengeFiles
            .Select(f => GameFactory.MapFromImage(f))
            .ToArray();


        CancellationTokenSource cts = new();

        Logger logger;
        while (true)
        {
            // Initial terminal setup
            Console.CursorVisible = false;
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("Press ESC or Q to exit");
            Console.ResetColor();

            try
            {
                logger = new Logger(1, testWorldMap.GetLength(1) + 3, Console.WindowWidth - 3, Console.WindowHeight - testWorldMap.GetLength(1) - 4, ConsoleColor.White, Console.BackgroundColor, ConsoleSync.sync);
                break;
            }
            catch
            {
                Console.Clear();
                Console.WriteLine("Terminal window is too small. Resize it and press any key to try again...");
                Console.ReadKey(true);
            }
        } 

        ConsoleVisualizer viz = new ConsoleVisualizer(new(Console.WindowWidth / 2 - (testWorldMap.GetLength(0) * 2 + 2) / 2, 0), ConsoleSync.sync);
        viz.AttachMap(testWorldMap);

        // Start test world server
        ConnectionHandler testConnectionHandler = new(testWorldMap, viz);
        Task testServerTask = testConnectionHandler.StartHttpServer(8080, cts.Token);
        testConnectionHandler.SessionConnected += new SessionConnectedLogger(logger, "Test world", ConsoleColor.White).Handler;
        logger.WriteLine("Test server started on port 8080", ConsoleColor.Yellow);

        // Start challenge world servers
        ConnectionHandler[] challangeConnectionHandlers = new ConnectionHandler[challangeMaps.Length];
        Task[] challengeServerTasks = new Task[challangeMaps.Length];
        for (int i = 0; i < challangeMaps.Length; i++)
        {
            challangeConnectionHandlers[i] = new ConnectionHandler(challangeMaps[i], null);
            challangeConnectionHandlers[i].SessionConnected += new SessionConnectedLogger(logger, $"Challenge {i + 1}", ConsoleColor.Green).Handler;
            challengeServerTasks[i] = challangeConnectionHandlers[i].StartHttpServer(8080 + i + 1, cts.Token);
            logger.WriteLine($"Challenge {i + 1} server started on port {8080 + i + 1}", ConsoleColor.Yellow);
        }

        // Wait until exit key is pressed
        while (!new[] { ConsoleKey.Escape, ConsoleKey.Q }.Contains(Console.ReadKey(true).Key))
        {
            // Pass
        }

        // Clear up the terminal
        Console.CursorVisible = true;
        Console.Clear();
        cts.Cancel();
    }
}

class SessionConnectedLogger
{
    private Logger logger;
    private string world;
    private ConsoleColor worldColor;

    public SessionConnectedLogger(Logger logger, string world, ConsoleColor worldColor)
    {
        this.logger = logger;
        this.world = world;
        this.worldColor = worldColor;
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

            logger.Write("Connected with '", ConsoleColor.White);
            if (e.SessionIdentifier != null)
                logger.Write(e.SessionIdentifier.IdentifierStr, e.SessionIdentifier.Color.Value);
            logger.Write("' into '", ConsoleColor.White);
            logger.Write(world, worldColor);
            logger.WriteLine($"'", ConsoleColor.White);

            if (e.GameSession == null)
            {
                logger.WriteLine("Connection without session detected!", ConsoleColor.White, ConsoleColor.Red);
                return;
            }
        }

        e.GameSession.AgentDied += new AgentDiedLogger(e, logger, world, worldColor).Handler;
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