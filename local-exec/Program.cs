using ExplorerGame.Base;
using ExplorerGame.Core;
using ExplorerGame.ConsoleVisualizer;
using ExplorerGame.Net;

Tile?[,] map = 
{
    { null,  null,  "||",  null,  null },
    { null,  "/=",  "][", "=\\", null },
    { null,  "| ",  null,  " |",  null },
    { null,  "\\=", "][", "=/",  null },
    { null,  null,  "||",  null,  null },
};

map = GameFactory.MapFromImage("resources/test-map.png");

Tile?[,] TransposeMap(Tile?[,] originalMap)
{
    int rows = originalMap.GetLength(0);
    int cols = originalMap.GetLength(1);
    Tile?[,] transposedMap = new Tile?[cols, rows];

    for (int i = 0; i < rows; i++)
        for (int j = 0; j < cols; j++)
            transposedMap[j, i] = originalMap[i, j];

    return transposedMap;
}

// map = TransposeMap(map);

Console.CursorVisible = false;
Console.Clear();

object sync = new object();
ConsoleVisualizer viz = new ConsoleVisualizer(new (Console.WindowWidth / 2 - (map.GetLength(0) * 2 + 2) / 2, 0), sync);
CancellationTokenSource cts = new();
ConnectionHandler connectionHandler = new(map, viz);

Logger logger = new Logger(1, map.GetLength(1) + 3, Console.WindowWidth - 3, Console.WindowHeight - map.GetLength(1) - 4, ConsoleColor.White, Console.BackgroundColor, sync);
connectionHandler.SessionConnected += LogSessionConnected;

Task serverTask = connectionHandler.StartHttpServer(8080, cts.Token);

RemoteGameSessionFactory factory = new("http://localhost:8080/", "Velky David");
SessionIdentifier id1 = new SessionIdentifier("()", ConsoleColor.Green);
SessionIdentifier id2 = new SessionIdentifier("<>", ConsoleColor.Blue);

RemoteGameSession session1 = factory.Create(id1);
RemoteGameSession session2 = factory.Create(id2);

try
{
    ConsoleKeyInfo keyInfo;
    do
    {
        keyInfo = Console.ReadKey(true);
        int dist = keyInfo.Modifiers.HasFlag(ConsoleModifiers.Shift) ? 2 : 1;

        MovementResult? movementResult1 = null;
        MovementResult? movementResult2 = null;
        switch (keyInfo.Key)
        {
            case ConsoleKey.UpArrow:
                movementResult1 = session1.Move(new(0, -dist));
                break;
            case ConsoleKey.DownArrow:
                movementResult1 = session1.Move(new(0, dist));
                break;
            case ConsoleKey.RightArrow:
                movementResult1 = session1.Move(new(dist, 0));
                break;
            case ConsoleKey.LeftArrow:
                movementResult1 = session1.Move(new(-dist, 0));
                break;

            case ConsoleKey.W:
                movementResult2 = session2.Move(new(0, -dist));
                break;
            case ConsoleKey.S:
                movementResult2 = session2.Move(new(0, dist));
                break;
            case ConsoleKey.D:
                movementResult2 = session2.Move(new(dist, 0));
                break;
            case ConsoleKey.A:
                movementResult2 = session2.Move(new(-dist, 0));
                break;
        }
        if (movementResult1.HasValue && !movementResult1.Value.MovedSuccessfully)
            logger.WriteLine($"Local agent 1 move failed: {session1.LastResponseMessage ?? "No message"}", ConsoleColor.White, ConsoleColor.Red);
        if (movementResult2.HasValue && !movementResult2.Value.MovedSuccessfully)
            logger.WriteLine($"Local agent 2 move failed: {session1.LastResponseMessage ?? "No message"}", ConsoleColor.White, ConsoleColor.Red);
    } while (keyInfo.Key != ConsoleKey.Escape);
}
catch (Exception ex)
{
    logger.WriteLine(ex.ToString(), ConsoleColor.White, ConsoleColor.Red);
}
finally
{
    Console.CursorVisible = true;
    Console.Clear();
    cts.Cancel();
}

void LogSessionConnected(object? sender, SessionConnectedEventArgs e)
{
    logger.Write("[", ConsoleColor.White);
    logger.Write(e.ClientUsername, ConsoleColor.Yellow);
    logger.Write(" @" + e.ClientID, ConsoleColor.DarkGray);
    logger.Write("] ", ConsoleColor.White);
    if (!e.Response.Value<bool>("success"))
    {
        logger.WriteLine("Agent connection failed", ConsoleColor.Red);
        return;
    }

    logger.Write("Connected with '");
    if (e.SessionIdentifier != null)
        logger.Write(e.SessionIdentifier.IdentifierStr, e.SessionIdentifier.Color.Value);
    logger.WriteLine("'");

    if (e.GameSession == null)
    {
        logger.WriteLine("Connection without session detected!", ConsoleColor.Red);
        return;
    }

    e.GameSession.AgentDied += new AgentDiedLogger(e, logger).Handler;
}

class AgentDiedLogger
{
    private SessionConnectedEventArgs sessionArgs;
    private Logger logger;

    public AgentDiedLogger(SessionConnectedEventArgs sessionArgs, Logger logger)
    {
        this.sessionArgs = sessionArgs;
        this.logger = logger;
    }

    public void Handler(object? sender, AgentDiedEventArgs e)
    {
        logger.Write("[", ConsoleColor.White);
        logger.Write(sessionArgs.ClientUsername, ConsoleColor.Yellow);
        logger.Write(" @" + sessionArgs.ClientID, ConsoleColor.DarkGray);
        logger.WriteLine($"] Died (reason: '{ e.DeathReason }')", ConsoleColor.White);
    }
}