/* 
 Example: Simple movement of an agent

 - Connects to the server and creates a single agent.
 - Assigns the agent a visual identifier (icon + color).
 - Demonstrates basic moves: right, left, up, down, and a jump.
 - Then loops forever, making the agent walk in a square.
 
 - The goal is to show how to connect, control movement, 
   and run continuous actions.
*/

// Add these usings
using ExplorerGame.Core;
using ExplorerGame.Net;

namespace ExampleSimple
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Prepare an object ("factory") that helps us create agents and connect to them
            string address = "http://127.0.0.1:8080/";
            string username = "Example";
            RemoteGameSessionFactory factory = new RemoteGameSessionFactory(address, username);

            // Prepare a graphical identifier that will be shown on the server screen
            // ! Everyone must have their own, it cannot happen that multiple people have the same one !
            string img = "[]";
            ConsoleColor color = ConsoleColor.Magenta;
            SessionIdentifier sessionIdentifier = new SessionIdentifier(img, color);

            // Create an agent and connect to it
            RemoteGameSession session = factory.Create(sessionIdentifier);

            // Data type used to define the direction of movement.
            //    Distance on the X axis ──┐   ┌── Distance on the Y axis
            //                             V   V
            Vector vector = new Vector(0, 0);

            session.Move(new Vector(1, 0)); // Move right
            Thread.Sleep(500);              // Wait 0.5 seconds (500ms)
            session.Move(new Vector(-1, 0)); // Move left
            Thread.Sleep(500);
            session.Move(new Vector(0, 1));  // Move up
            Thread.Sleep(500);
            session.Move(new Vector(0, -1)); // Move down
            Thread.Sleep(500);

            session.Move(new Vector(2, 0));  // Jump right
            Thread.Sleep(500);

            // Walk in a square forever
            while (true)
            {
                MoveNTimes(4, new Vector(1, 0), session);
                MoveNTimes(4, new Vector(0, 1), session);
                MoveNTimes(4, new Vector(-1, 0), session);
                MoveNTimes(4, new Vector(0, -1), session);
            }
        }

        static void MoveNTimes(int n, Vector movement, RemoteGameSession session)
        {
            for (int i = 0; i < n; i++)
                session.Move(movement);
        }
    }
}
