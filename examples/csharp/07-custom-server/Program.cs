/* 
 Example: Running a simple game server

 - Builds a small 5x5 map with a few traps.
 - Sets up a console view that shows the map and a logger underneath it.
 - Starts a local HTTP server on port 8080 for agents to connect.
 - Logs each new connection with the agent’s username and ID.
 - Keeps running until ESC is pressed, then shuts down cleanly.

 - The goal is to show how to host a basic game server and visualize it in the console.
*/

using ExplorerGame.Core;
using ExplorerGame.Net;
using ExplorerGame.ConsoleVisualizer;

class Program
{
    static void Main()
    {
        // Create a simple 5x5 map with a few traps (marked as "##")
        Tile?[,] map = new Tile?[5, 5];
        map[1, 1] = "##";
        map[2, 3] = "##";
        map[4, 0] = "##";

        // Clear the console and hide the cursor for a clean view
        Console.Clear();
        Console.CursorVisible = false;

        // Object used to synchronize console output 
        // (so map and log don’t overwrite each other)
        object syncObject = new object();

        // Create a logger below the map (with a 2-line gap for the frame => map_height + gap)
        int loggerTop = 5 + 2; 
        Logger logger = new Logger(1, loggerTop, Console.WindowWidth - 2, 10, 
                                   ConsoleColor.White, Console.BackgroundColor, syncObject);

        // Create a visualizer for the map and attach the map to it
        ConsoleVisualizer viz = new ConsoleVisualizer(new Vector(1, 0), syncObject);
        viz.AttachMap(map);

        // Create a connection handler (server) to handle agent connections and movements
        ConnectionHandler server = new ConnectionHandler(map, viz);

        // Example of using the logger:
        // Log every new connection with username and session ID
        server.SessionConnected += (sender, e) =>
        {
            lock (syncObject)
            {
                logger.Write("[", ConsoleColor.White);
                logger.Write(e.ClientUsername, ConsoleColor.Yellow);
                logger.Write("] connected with ID ", ConsoleColor.White);
                if (e.SessionIdentifier != null)
                    logger.WriteLine(e.SessionIdentifier.Identifier, e.SessionIdentifier.Color);
                else
                    logger.WriteLine("(unknown ID)", ConsoleColor.Red);
            }
        };

        // Token to stop the server later
        CancellationTokenSource cancelToken = new CancellationTokenSource();

        // Run the HTTP server on port 8080 in a background task
        Task.Run(() => server.StartHttpServer(8080, cancelToken.Token));

        // Log server status messages
        logger.WriteLine("Server running on port 8080", ConsoleColor.Green);
        logger.WriteLine("Press ESC to exit", ConsoleColor.DarkGray);

        // Wait until the ESC key is pressed
        while (Console.ReadKey(true).Key != ConsoleKey.Escape) {}

        // Stop the server and restore the console
        cancelToken.Cancel();
        Console.Clear();
        Console.CursorVisible = true;
    }
}
