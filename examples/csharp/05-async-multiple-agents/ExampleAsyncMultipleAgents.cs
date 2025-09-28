/* 
 Example: Asynchronous movement with multiple agents

 - Starts several agents at once and makes them move asynchronously.
 - Each agent picks a random direction from a list (right and up are more likely).
 - Continuously checks if each agent has finished its move.
 - If an agent dies, it is instantly recreated so the group size stays the same.
 - The result is a swarm of agents moving around at the same time without waiting
   for one another.

 - The goal is to show how to coordinate many agents in parallel using async moves.
*/

using ExplorerGame.Net;
using ExplorerGame.Core;

namespace ExampleAsyncMultipleAgents
{
    internal class Program
    {
        static void Main(string[] args)
        {
            RemoteGameSessionFactory factory = new RemoteGameSessionFactory("http://127.0.0.1:8080/", "Example");

            const int AGENT_CNT = 5; // How many agents we want

            // Create the agents and store their sessions
            RemoteGameSession[] sessions = new RemoteGameSession[AGENT_CNT];
            for (int i = 0; i < sessions.Length; i++)
            {
                sessions[i] = factory.Create(new SessionIdentifier("[" + i, ConsoleColor.Magenta));
            }

            // Define possible movements
            // (some are repeated to make them more likely to be chosen)
            Vector[] vecs = new Vector[]
            {
                new Vector(1, 0), // right (higher chance)
                new Vector(1, 0),
                new Vector(1, 0),

                new Vector(0, 1), // up (higher chance)
                new Vector(0, 1),
                new Vector(0, 1),

                new Vector(-1, 0), // left
                new Vector(0, -1)  // down
            };

            Random rnd = new Random();

            // Storage of async results for each agent
            AsyncMovementResult[] results = new AsyncMovementResult[AGENT_CNT];

            // Start the first move for each agent
            for (int i = 0; i < AGENT_CNT; i++)
            {
                Vector movement = vecs[rnd.Next(0, vecs.Length)];
                results[i] = sessions[i].MoveAsync(movement);
            }

            // Keep the agents moving forever
            while (true)
            {
                // Go through all agents
                for (int i = 0; i < results.Length; i++)
                {
                    AsyncMovementResult resultHandle = results[i];

                    // Check if the current move is finished
                    if (resultHandle.Ready)
                    {
                        // If the agent died, create a new one
                        if (!resultHandle.MovementResult.Value.IsAgentAlive)
                            sessions[i] = factory.Create(new SessionIdentifier("[" + i, ConsoleColor.Magenta));

                        // Start a new random move for this agent
                        Vector movement = vecs[rnd.Next(0, vecs.Length)];
                        results[i] = sessions[i].MoveAsync(movement);
                    }
                }
            }
        }
    }
}
