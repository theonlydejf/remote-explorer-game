/* 
 Example: Feedback and error handling

 - Connects to a single agent and checks if it is alive.
 - Demonstrates how each move returns feedback about success 
   and survival.
 - Covers different cases: invalid moves, collisions, 
   or death.
 ADVANCED SECTION:
 - Shows how to handle server messages when something goes 
   wrong (like inactivity timeouts).

 - The goal is to teach how to read movement results and 
   respond to errors in the game.
*/

using ExplorerGame.Core;
using ExplorerGame.Net;

namespace ExampleFeedback
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Connect to the server and create one agent
            RemoteGameSessionFactory factory = new RemoteGameSessionFactory("http://127.0.0.1:8080/", "Example");
            RemoteGameSession session = factory.Create(new SessionIdentifier("[]", ConsoleColor.Magenta));

            // The simplest way to check if the agent is alive
            // Session automatically updates this value after each action
            if (session.IsAgentAlive)
            {
                Console.WriteLine("Agent is alive");
            }
            else
            {
                Console.WriteLine("Agent is not alive");
            }

            // Each call to session.Move(...) returns information about the result
            MovementResult result = session.Move(new Vector(1, 0)); // Everything succeeds

            // Invalid move => unsuccessful move but survives
            // result = session.Move(new Vector(6, -9)); // Uncomment me to try me out

            // Agent hits a wall => successful move but does not survive
            // result = session.Move(new Vector(0, -1)); // Uncomment me to try me out

            if (result.IsAgentAlive) // Check if the agent survived the move
            {
                Console.WriteLine("Agent survived the move");
            }
            else
            {
                Console.WriteLine("Agent did not survive the move");
            }

            if (result.MovedSuccessfully) // Check if the move was successful
            {
                Console.WriteLine("Move was successful");
            }
            else
            {
                // Can happen if trying to communicate with an agent that is already dead
                // or performing an invalid move (e.g., new Vector(10, -68))
                Console.WriteLine("Move was not successful");
            }

            // Each movement discovers a tile from the map.
            // Tile is null if the the tile is empty, when the agent wandered out of them
            // or if the movement was not successful
            Tile? discoveredTile = result.DiscoveredTile;

            if (discoveredTile.HasValue)
            {
                // Print the text representation of the tile
                Console.WriteLine("Discovered tile: " + discoveredTile.ToString());
            }

            // Session keeps track of last discovered tile (similar to session.IsAgentAlive)
            if (session.DiscoveredTile.HasValue)
            {
                Console.WriteLine("Discovered tile: " + session.DiscoveredTile.ToString());
            }

            /////////// ADVANCED ///////////

                Console.WriteLine("Waiting...");
            Thread.Sleep(6100); // Wait until the server kicks the agent for inactivity
            MovementResult kickedMoveResult = session.Move(new Vector(1, 0));
            Console.WriteLine($"Alive: {kickedMoveResult.IsAgentAlive}, Move was successful: {kickedMoveResult.MovedSuccessfully}");

            // What to do if I don't know why the agent suddenly died and I can't move it anymore?
            // The server sends back a message explaining why the move failed.

            // Check if a message was received (message is not guaranteed with any response)
            if (session.LastResponseMessage != null)
            {
                // Print the message
                Console.WriteLine("Message: " + session.LastResponseMessage);
            }
            else
            {
                Console.WriteLine("No message received");
            }
        }
    }
}
