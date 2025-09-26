/* 
 Example: Asynchronous movement and polling

 - Starts a move asynchronously and continues doing other work.
 - Polls the async handle until the move finishes (non-blocking pattern).
 - Reads the final movement result (success + survival) once ready.

 - The goal is to demonstrate how to start actions without waiting,
   keep the app responsive, and handle results when they arrive.
*/

using ExplorerGame.Net;
using ExplorerGame.Core;

namespace ExampleAsync
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Connect to the server and create one agent
            RemoteGameSessionFactory factory = new RemoteGameSessionFactory("http://127.0.0.1:8080/", "Example");
            RemoteGameSession session = factory.Create(new SessionIdentifier("[]", ConsoleColor.Magenta));

            Console.WriteLine("Movement started...");

            // Ask the agent to move, but do it asynchronously
            // (the game starts moving the agent, but our program does not wait here)
            AsyncMovementResult asyncResult = session.MoveAsync(new Vector(1, 0));

            // We can keep doing something else while the move is still in progress
            Console.WriteLine("Doing something else while the movement completes...");

            // Check every little while if the move has finished
            // asyncResult.Ready will turn true once the move is done
            while (!asyncResult.Ready)
            {
                Console.WriteLine("Waiting for the movement to finish...");
                Thread.Sleep(20); // pause for 20 milliseconds before checking again
            }

            // Now that the move is finished, we can read the result
            bool isAlive = asyncResult.MovementResult.Value.IsAgentAlive; // Did the agent survive?
            bool moveSuccessful = asyncResult.MovementResult.Value.MovedSuccessfully; // Did the move succeed?
            Console.WriteLine($"Alive: {isAlive}, Move was successful: {moveSuccessful}");
        }
    }
}
