using System;
using ExplorerGame.Net;
using ExplorerGame.Core;

namespace ExampleAsyncMultipleAgents
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
                sessions[i] = factory.Create(new SessionIdentifier("[" + i, ConsoleColor.Magenta));
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
            AsyncMovementResult[] results = new AsyncMovementResult[AGENT_CNT];

            // Udelej prvni pohyb
            for (int i = 0; i < AGENT_CNT; i++)
            {
                Vector movement = vecs[rnd.Next(0, vecs.Length)];
                results[i] = sessions[i].MoveAsync(movement);
            }

            while (true)
            {
                // Pro kazdy vysledek pohybu
                for (int i = 0; i < results.Length; i++)
                {
                    // Zkontroluj jestli byl pohyb uz dokoncen
                    if (results[i].Ready)
                    {
                        // Pokud umrel -> vytvor noveho agenta
                        if (!results[i].MovementResult.Value.IsAgentAlive)
                            sessions[i] = factory.Create(new SessionIdentifier("[" + i, ConsoleColor.Magenta));

                        // Znovu s nim pohni
                        Vector movement = vecs[rnd.Next(0, vecs.Length)];
                        results[i] = sessions[i].MoveAsync(movement);
                    }
                }
            }
        }
    }
}