using System;

////////// Potreba pridat tyto using //////////
using ExplorerGame.Core;
using ExplorerGame.Net;

namespace ExampleSimple
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Priprav objekt ("tovarnu"), ktera nam pomuze vytvaret agenty a pripojovat se k nim
            string address = "http://127.0.0.1:8080/";
            string username = "Ukazka";
            RemoteGameSessionFactory factory = new RemoteGameSessionFactory(address, username);

            // Priprav graficky identifikator, ktery se ukaze na obrazovce serveru
            // ! Kazdy musi mit svuj, nemuze se stat ze vice lidi bude mit stejny !
            string img = "[]";
            ConsoleColor color = ConsoleColor.Magenta;
            SessionIdentifier sessionIdentifier = new SessionIdentifier(img, color);

            // Vytvori agenta a pripoji se k nemu
            RemoteGameSession session = factory.Create(sessionIdentifier);

            // Datovy typ kterym definujeme smer pohybu.
            //    Vzdalenost v ose X ──┐   ┌── Vzdalenost v ose Y
            //                         V   V
            Vector vector = new Vector(0, 0);

            session.Move(new Vector(1, 0)); // Pohni se doprava
            Thread.Sleep(500);              // Cekej 0.5 vteriny (500ms)
            session.Move(new Vector(-1, 0)); // Pohni se doleva
            Thread.Sleep(500);
            session.Move(new Vector(0, 1));  // Pohni se nahoru
            Thread.Sleep(500);
            session.Move(new Vector(0, -1)); // Pohni se dolu
            Thread.Sleep(500);

            session.Move(new Vector(2, 0));  // Skoc doprava
            Thread.Sleep(500);

            // Do nekonecna chod ve ctverci
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