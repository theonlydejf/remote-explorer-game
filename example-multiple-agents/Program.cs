using System;
using ExplorerGame.Net;
using ExplorerGame.Core;

namespace ExampleMultipleAgents
{
    internal class Program
    {
        static void Main(string[] args)
        {
            RemoteGameSessionFactory factory = new RemoteGameSessionFactory("http://127.0.0.1:8080/", "Ukazka");

            const int AGENT_CNT = 5;

            // Vytvor agenty
            RemoteGameSession[] sessions = new RemoteGameSession[AGENT_CNT];
            for (int i = 0; i < sessions.Length; i++)
            {
                sessions[i] = factory.Create(new SessionIdentifier("[" + i, ConsoleColor.Blue));
            }

            // Pohyby co chci aby agenti umeli
            Vector[] vecs = new Vector[]
            {
                new Vector(1, 0), // Vetsi sance ze vybere tenhle pohyb
                new Vector(1, 0),
                new Vector(1, 0), 

                new Vector(0, 1), // Vetsi sance ze vybere tenhle pohyb
                new Vector(0, 1),
                new Vector(0, 1),

                new Vector(-1, 0),

                new Vector(0, -1)
            };

            Random rnd = new Random();
            while (true)
            {
                // Pro kazdeho agenta
                for (int i = 0; i < sessions.Length; i++)
                {
                    // Pohni s nim nahodnym smerem
                    Vector movement = vecs[rnd.Next(0, vecs.Length)];
                    MovementResult result = sessions[i].Move(movement);

                    // Pokud pri pohybu zemrel, vytvor noveho
                    if (!result.IsAgentAlive)
                    {
                        sessions[i] = factory.Create(new SessionIdentifier("[" + i, ConsoleColor.Blue));
                    }
                }
            }
        }
    }
}
