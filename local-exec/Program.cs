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
    {
        for (int j = 0; j < cols; j++)
        {
            transposedMap[j, i] = originalMap[i, j];
        }
    }

    return transposedMap;
}

map = GameFactory.MapFromImage("test-img.png");//TransposeMap(map);

Console.CursorVisible = false;
Console.Clear();

ConsoleVisualizer viz = new ConsoleVisualizer(new (0, 0));

CancellationTokenSource cts = new();
ConnectionHandler connectionHandler = new(map, viz);
Task serverTask = connectionHandler.StartHttpServer("http://localhost:8080/", cts.Token);

RemoteGameSessionFactory factory = new("http://localhost:8080/");

// LocalGameSession session1 = new LocalGameSession(map);
// SessionIdentifier id1 = new SessionIdentifier("🐶", ConsoleColor.Green);
// LocalGameSession session2 = new LocalGameSession(map);
// SessionIdentifier id2 = new SessionIdentifier("🐼", ConsoleColor.Blue);

// viz.AttachGameSession(session1, id1);
// viz.AttachGameSession(session2, id2);

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
    } while (keyInfo.Key != ConsoleKey.Escape && (session1.IsAgentAlive || session2.IsAgentAlive));
}
finally
{
    Console.CursorVisible = true;
    Console.ReadKey();
}