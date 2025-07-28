using ExplorerGame.Base;
using ExplorerGame.Core;
using ExplorerGame.ConsoleVisualizer;
using ExplorerGame.Net;

Tile?[,] map = GameFactory.MapFromImage("resources/test-map.png");
Tile?[][,] challangeMaps =
{
    GameFactory.MapFromImage("resources/challenge-1.png"),
    GameFactory.MapFromImage("resources/challenge-2.png"),
    GameFactory.MapFromImage("resources/challenge-3.png"),
    GameFactory.MapFromImage("resources/challenge-4.png"),
    GameFactory.MapFromImage("resources/challenge-5.png"),
    GameFactory.MapFromImage("resources/challenge-6.png"),
    GameFactory.MapFromImage("resources/challenge-7.png")
};

Console.CursorVisible = false;
Console.Clear();


Logger logger = new Logger(1, map.GetLength(1) + 3, Console.WindowWidth - 3, Console.WindowHeight - map.GetLength(1) - 4, ConsoleColor.White, Console.BackgroundColor, ConsoleSync.sync);

CancellationTokenSource cts = new();

ConsoleVisualizer viz = new ConsoleVisualizer(new (Console.WindowWidth / 2 - (map.GetLength(0) * 2 + 2) / 2, 0), ConsoleSync.sync);
ConnectionHandler testConnectionHandler = new(map, viz);
testConnectionHandler.SessionConnected += new SessionConnectedLogger(logger, "Test world", ConsoleColor.White).Handler;
Task testServerTask = testConnectionHandler.StartHttpServer(8080, cts.Token);

logger.WriteLine("Test server started on port 8080", ConsoleColor.Yellow);

ConnectionHandler[] challangeConnectionHandlers = new ConnectionHandler[challangeMaps.Length];
Task[] challengeServerTasks = new Task[challangeMaps.Length];
for (int i = 0; i < challangeMaps.Length; i++)
{
    challangeConnectionHandlers[i] = new ConnectionHandler(challangeMaps[i], null);
    challangeConnectionHandlers[i].SessionConnected += new SessionConnectedLogger(logger, $"Challenge {i + 1}", ConsoleColor.Green).Handler;
    challengeServerTasks[i] = challangeConnectionHandlers[i].StartHttpServer(8080 + i + 1, cts.Token);
    logger.WriteLine($"Challenge {i + 1} server started on port {8080 + i + 1}", ConsoleColor.Yellow);
}

RemoteGameSessionFactory factory = new("http://localhost:8080/", "Velky David");
SessionIdentifier id1 = new SessionIdentifier("()", ConsoleColor.Green);
RemoteGameSession session1 = factory.Create(id1);

while (Console.ReadKey(true).Key != ConsoleKey.Escape)
{
    // Pass
}

Console.CursorVisible = true;
Console.Clear();
cts.Cancel();

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
                logger.Write(e.SessionIdentifier.Identifier, e.SessionIdentifier.Color);
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

public static class ConsoleSync
{
    public static object sync = new object();
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