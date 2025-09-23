using System;
using System.Drawing;
using ExplorerGame.Core;
using ExplorerGame.Net;

namespace ExampleFeedback
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Pripoj se k agentovi
            RemoteGameSessionFactory factory = new RemoteGameSessionFactory("http://127.0.0.1:8080/", "Ukazka");
            RemoteGameSession session = factory.Create(new SessionIdentifier("[]", ConsoleColor.Magenta));

            // Nejjednodussi zpusob jak zjistit jestli agent zije
            // Session automaticky aktualizuje tuhle hodnotu po kazde provedene akci
            if (session.IsAgentAlive)
            {
                Console.WriteLine("Agent zije");
            }
            else
            {
                Console.WriteLine("Agent nezije");
            }

            // Kazde volani session.Move(...) vraci informaci o tom jak probehlo
            MovementResult result = session.Move(new Vector(1, 0)); // Vse probehne uspesne

            // Agent narazi do zdi
            //  => uspesny pohyb ale neprezije
            // MovementResult result = session.Move(new Vector(-1, 0));

            // Nepovoleny pohyb
            //  => neuspesny pohyb ale prezije
            // MovementResult result = session.Move(new Vector(6, -9));

            if (result.IsAgentAlive) // Zjisti jestli agent dany pohyb prezil
            {
                Console.WriteLine("Agent prezil pohyb");
            }
            else
            {
                Console.WriteLine("Agent neprezil pohyb");
            }

            if (result.MovedSuccessfully) // Zjisti jestli pohyb probehl
            {
                Console.WriteLine("Pohyb probehl uspesne");
            }
            else
            {
                // Muze se stat pokud se snazim komunikovat s agentem co uz nezije
                // nebo provadim nepovoleny pohyb (napriklad new Vector(10, -68))
                Console.WriteLine("Pohyb neprobehl uspesne");
            }

            /////////// POKROCILE ///////////

            Console.WriteLine("Cekam...");
            Thread.Sleep(6100); // Pockam nez server agenta vyhodi za dlouhou neaktivitu
            MovementResult killedMoveResult = session.Move(new Vector(1, 0));
            Console.WriteLine($"Zije: {killedMoveResult.IsAgentAlive}, Pohyb byl uspesny: {killedMoveResult.MovedSuccessfully}");

            // Co delat pokud nevim proc agent z niceho nic umrel a nemuzu s nim najednou hybat?
            // Server pri neuspesnem pokusu o pohyb posila zpet zpravu proc to nemohl udelat.

            // Zkontroluji jestli mi zprava prisla (ne vzdy musi prijit)
            if (session.LastResponseMessage != null)
            {
                // Vypisu zpravu
                Console.WriteLine("Zprava: " + session.LastResponseMessage);
            }
        }
    }
}
