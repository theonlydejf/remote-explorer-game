using ExplorerGame.Base;
using ExplorerGame.Core;
using ExplorerGame.Net;
using ExplorerGame.ConsoleVisualizer;

class Program
{
    static void Main()
    {
        // Vytvoreni jednoduche mapy 5x5 s nekolika pastmi
        Tile?[,] map = new Tile?[5, 5];
        map[1, 1] = "##";
        map[2, 3] = "##";
        map[4, 0] = "##";

        // Skryje kurzor a vycisti konzoli
        Console.Clear();
        Console.CursorVisible = false;

        // Objekt pro synchronizaci vypisu do konzole
        object syncObject = new object();

        // Vytvoreni loggeru pod mapou
        int loggerTop = 5 + 2; // 5 radku mapy + ramecek
        Logger logger = new Logger(1, loggerTop, Console.WindowWidth - 2, 10, ConsoleColor.White, Console.BackgroundColor, syncObject);

        // Vytvoreni vizualizace mapy
        ConsoleVisualizer viz = new ConsoleVisualizer(new Vector(1, 0), syncObject);
        viz.AttachMap(map);

        // Vytvoreni serveru pro obsluhu pripojeni a pohybu
        ConnectionHandler server = new ConnectionHandler(map, viz);

        // Prikad vyuziti loggeru -> zaznamenej kazde nove pripojeni 
        server.SessionConnected += (sender, e) =>
        {
            lock (syncObject)
            {
                logger.Write("[", ConsoleColor.White);
                logger.Write(e.ClientUsername, ConsoleColor.Yellow);
                logger.Write("] pripojen s ID ", ConsoleColor.White);
                if (e.SessionIdentifier != null)
                    logger.WriteLine(e.SessionIdentifier.Identifier, e.SessionIdentifier.Color);
                else
                    logger.WriteLine("(nezname ID)", ConsoleColor.Red);
            }
        };

        // Token pro zastaveni serveru
        CancellationTokenSource cancelToken = new CancellationTokenSource();

        // Spusteni HTTP serveru na portu 8080
        Task.Run(() => server.StartHttpServer(8080, cancelToken.Token));

        logger.WriteLine("Server bezi na portu 8080", ConsoleColor.Green);
        logger.WriteLine("Stiskni ESC pro ukonceni", ConsoleColor.DarkGray);

        // Ceka na stisknuti klavesy ESC
        while (Console.ReadKey(true).Key != ConsoleKey.Escape) {}

        // Zastavi server a vycisti konzoli
        cancelToken.Cancel();
        Console.Clear();
        Console.CursorVisible = true;
    }
}
