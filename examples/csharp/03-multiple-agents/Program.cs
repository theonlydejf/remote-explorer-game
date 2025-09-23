/* 
 Example: Controlling multiple agents

 - Shows how to manage several agents at the same time.
 - Agents move in random directions, with a higher chance 
   of going right or up.
 - Illustrates automatic recovery: when an agent dies, 
   a new one is created instantly.
 - The result looks like a swarm of agents wandering the map 
   without stopping.

 - The goal is to demonstrate how to coordinate multiple 
   sessions, use randomness, and keep agents running even 
   after failure.
*/

using ExplorerGame.Net;
using ExplorerGame.Core;

namespace ExampleMultipleAgents
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Connect to the server and create one agent
            RemoteGameSessionFactory factory = new RemoteGameSessionFactory("http://127.0.0.1:8080/", "Example");

            const int AGENT_CNT = 5;

            // Create agents and store their sessions
            RemoteGameSession[] sessions = new RemoteGameSession[AGENT_CNT];
            for (int i = 0; i < sessions.Length; i++)
            {
                sessions[i] = factory.Create(new SessionIdentifier("[" + i, ConsoleColor.Magenta));
            }

            // Define possible movements
            // (some are repeated to make them more likely to be chosen)
            Vector[] vecs = new Vector[]
            {
                new Vector(1, 0), // Higher chance to select this movement
                new Vector(1, 0),
                new Vector(1, 0), 

                new Vector(0, 1), // Higher chance to select this movement
                new Vector(0, 1),
                new Vector(0, 1),

                new Vector(-1, 0),

                new Vector(0, -1)
            };

            Random rnd = new Random();
            while (true)
            {
                // For each agent
                for (int i = 0; i < sessions.Length; i++)
                {
                    // Move it in a random direction
                    Vector movement = vecs[rnd.Next(0, vecs.Length)];
                    MovementResult result = sessions[i].Move(movement);

                    // If the agent died during movement, create a new one
                    if (!result.IsAgentAlive)
                    {
                        sessions[i] = factory.Create(new SessionIdentifier("[" + i, ConsoleColor.Magenta));
                    }
                }
            }
        }
    }
}
