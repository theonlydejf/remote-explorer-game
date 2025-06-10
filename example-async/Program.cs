using System;
using ExplorerGame.Net;
using ExplorerGame.Core;

namespace ExampleAsync
{
    internal class Program
    {
        static void Main(string[] args)
        {
            RemoteGameSessionFactory factory = new RemoteGameSessionFactory("http://127.0.0.1:8080/", "Ukazka");
            RemoteGameSession session = factory.Create(new SessionIdentifier("[]", ConsoleColor.Magenta));

            Console.WriteLine("Pohyb zacal...");
            AsyncMovementResult asyncResult = session.MoveAsync(new Vector(1, 0));

            Console.WriteLine("Delam neco mezi tim nez se pohyb dokonci...");

            while (!asyncResult.Ready) // asyncResult.Ready se zmeni na true az bude pohyb dokoncen
            {
                Console.WriteLine("Cekam nez pohyb skonci...");
                Thread.Sleep(20);
            }

            // asyncResult.MovementResult.Value -> zde bude ulozen vysledek pohybu
            bool isAlive = asyncResult.MovementResult.Value.IsPlayerAlive;
            bool moveSuccessful = asyncResult.MovementResult.Value.MovedSuccessfully;
            Console.WriteLine($"Zije: {isAlive}, Pohyb byl uspesny: {moveSuccessful}");
        }
    }
}