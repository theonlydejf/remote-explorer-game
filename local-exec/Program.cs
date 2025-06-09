using ExplorerGame.Base;
using ExplorerGame.Core;
using ExplorerGame.ConsoleVisualizer;
using ExplorerGame.Net;
using ExplorerGame.Net.Session;

Tile?[,] map = 
{
    { null,  null,  "||",  null,  null },
    { null,  "/=",  "][", "=\\", null },
    { null,  "| ",  null,  " |",  null },
    { null,  "\\=", "][", "=/",  null },
    { null,  null,  "||",  null,  null },
};

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

map = TransposeMap(map);

Console.CursorVisible = false;
Console.Clear();

object sync = new object();
ConsoleVisualizer viz = new ConsoleVisualizer(new (0, 0), sync);
CancellationTokenSource cts = new();
ConnectionHandler connectionHandler = new(map, viz);

Logger logger = new Logger(20, 0, 50, 10, ConsoleColor.White, ConsoleColor.Red, sync);
connectionHandler.SessionConnected += LogSessionConnected;

Task serverTask = connectionHandler.StartHttpServer("http://localhost:8080/", cts.Token);

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

        switch (keyInfo.Key)
        {
            case ConsoleKey.UpArrow:
                session1.Move(new(0, -dist));
                break;
            case ConsoleKey.DownArrow:
                session1.Move(new(0, dist));
                break;
            case ConsoleKey.RightArrow:
                session1.Move(new(dist, 0));
                break;
            case ConsoleKey.LeftArrow:
                session1.Move(new(-dist, 0));
                break;

            case ConsoleKey.W:
                session2.Move(new(0, -dist));
                break;
            case ConsoleKey.S:
                session2.Move(new(0, dist));
                break;
            case ConsoleKey.D:
                session2.Move(new(dist, 0));
                break;
            case ConsoleKey.A:
                session2.Move(new(-dist, 0));
                break;
        }
    } while (keyInfo.Key != ConsoleKey.Escape && (session1.IsAgentAlive));
}
finally
{
    Console.CursorVisible = true;
    Console.ReadKey();
}

void LogSessionConnected(object? sender, SessionConnectedEventArgs e)
{
    logger.Write("[", ConsoleColor.White);
    logger.Write(e.ClientUsername, ConsoleColor.Yellow);
    logger.Write(" @ " + e.ClientID, ConsoleColor.DarkGray);
    logger.Write("] ", ConsoleColor.White);
    if (!e.Response.Value<bool>("success"))
    {
        logger.WriteLine("Agent connection failed", ConsoleColor.Red);
        return;
    }

    logger.Write("Connected with '");
    if (e.SessionIdentifier != null)
        logger.Write(e.SessionIdentifier.Identifier, e.SessionIdentifier.Color);
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
        logger.Write(" @ " + sessionArgs.ClientID, ConsoleColor.DarkGray);
        logger.Write($"] Died (reason: '{ e.DeathReason }')", ConsoleColor.White);
    }
}