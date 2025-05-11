using System.Diagnostics.Contracts;
using System.Text.RegularExpressions;
using ExplorerGame.Base;
using ExplorerGame.Core;

namespace ExplorerGame.ConsoleVisualizer;

public class SessionIdentifier
{
    public static readonly SessionIdentifier ERROR_IDENTIFIER = new() { Identifier = "EE", Color = ConsoleColor.Red};
    public const ConsoleColor SESSION_COUNTER_COLOR = ConsoleColor.Yellow;

    private static (Regex, ConsoleColor)[]? __RESERVED_IDENTIFIERS;
    private static (Regex, ConsoleColor)[] RESERVED_IDENTIFIERS 
    { 
        get 
        {
            if(__RESERVED_IDENTIFIERS == null)
            {
                __RESERVED_IDENTIFIERS = new (Regex, ConsoleColor)[2]
                {
                    (new Regex(ERROR_IDENTIFIER.Identifier), ERROR_IDENTIFIER.Color),
                    (new Regex(@"\d+|Hi"), SESSION_COUNTER_COLOR)
                };
            }
            return __RESERVED_IDENTIFIERS;
        }
    }

    public static bool IsReserved(SessionIdentifier identifier, Tile?[,]? map = null)
    {
        if(map != null && identifier.Color == ConsoleColor.White)
        {
            foreach(Tile? tile in map)
            {
                if(tile.ToString() == identifier.Identifier)
                    return true;
            }
        }

        foreach(var idSpec in RESERVED_IDENTIFIERS)
        {
            if(idSpec.Item2 == identifier.Color &&
                idSpec.Item1.IsMatch(identifier.Identifier))
                return true;
        }

        return false;
    }

    public ConsoleColor Color { get; set; }
    public string Identifier { get; set; }

    internal SessionIdentifier()
    {
        Identifier = "??";
        Color = ConsoleColor.White;
    }

    public SessionIdentifier(string identifier, ConsoleColor color = ConsoleColor.White, Tile?[,]? map = null)
    {
        Identifier = identifier;
        Color = color;

        if(identifier.Length > 2)
            throw new ArgumentException("Identifier can be 2 bytes at most");
        if(color == ConsoleColor.Black)
            throw new ArgumentException("Invalid identifier color");
        if(IsReserved(this, map))
            throw new ArgumentException("This identifier is reserved");
    }
}

public class ConsoleVisualizer
{
    private Dictionary<LocalGameSession, SessionIdentifier> gameSessions = new();
    private Vector windowLocation;
    private Vector origin;
    private object consoleLock;
    Tile?[,] map = new Tile?[0, 0];

    public ConsoleVisualizer(Vector windowLocation, object? consoleLock = null)
    {
        this.windowLocation = windowLocation;
        origin = windowLocation + new Vector(1, 1);
        if(consoleLock != null)
            this.consoleLock = consoleLock;
        else
            this.consoleLock = new object();
    }

    public bool AttachGameSession(LocalGameSession gameSession, SessionIdentifier identifier) => AttachGameSession(gameSession, identifier, Vector.Zero);

    public bool AttachGameSession(LocalGameSession gameSession, SessionIdentifier identifier, Vector agentLocation)
    {
        if (gameSessions.Count > 0 && !gameSessions.First().Key.map.Equals(gameSession.map))
            throw new ArgumentException("Trying to attach two game sessions with different maps onto one visualizer");

        if (gameSessions.ContainsKey(gameSession))
            return false;
        
        gameSession.AgentDied += OnAgentDied;
        gameSession.AgentMoved += OnAgentMoved;

        gameSessions.Add(gameSession, identifier);
        if(gameSessions.Count == 1)
        {
            map = gameSession.map;
            PrintBorder();
            UpdateWindow();
        }
        else
            UpdatePixel(gameSession.AgentLocation);
        return true;
    }

    private void OnAgentDied(object? sender, EventArgs e)
    {
        if(sender == null)
            return; // Ignore unknown senders

        if (sender is LocalGameSession session)
        {
            gameSessions.Remove(session);
            UpdatePixel(session.AgentLocation);
        }
    }

    private void OnAgentMoved(object? sender, AgentMovementEventArgs e)
    {
        if(sender == null)
            return; // Ignore unknown senders
        UpdatePixel(e.AgentPreviousLocation);
        UpdatePixel(e.AgentCurrentLocation);
    }

    private void UpdateWindow()
    {
        int w = map.GetLength(0);
        int h = map.GetLength(1);

        for(int y = 0; y < h; y++)
            for(int x = 0; x < w; x++)
                UpdatePixel(new (x, y));
    }

    private void UpdatePixel(Vector loc)
    {
        if (loc.X < 0 || loc.X >= map.GetLength(0) ||
            loc.Y < 0 || loc.Y >= map.GetLength(1))
        {
            return;
        }

        int agentCnt = 0;
        SessionIdentifier id = SessionIdentifier.ERROR_IDENTIFIER;
        foreach(var keyVal in gameSessions)
        {
            if(keyVal.Key.AgentLocation == loc)
            {
                agentCnt++;
                id = keyVal.Value;
            }
        }
        lock(consoleLock)
        {
            Console.SetCursorPosition(origin.X + loc.X*2, origin.Y + loc.Y);
            if(agentCnt > 1)
            {
                Console.ForegroundColor = SessionIdentifier.SESSION_COUNTER_COLOR;
                if(agentCnt > 99)
                    Console.Write("Hi");
                else
                    Console.Write(agentCnt.ToString().PadRight(2, ' '));
                return;
            }

            if(agentCnt == 1)
            {
                Console.ForegroundColor = id.Color;
                Console.Write(id.Identifier);
                return;
            }

            Console.ResetColor();
            if(map[loc.X, loc.Y].HasValue)
                Console.Write(map[loc.X, loc.Y].ToString());
            else
                Console.Write("  ");
        }
    }

    private void PrintBorder()
    {
        int w = map.GetLength(0);
        int h = map.GetLength(1);

        lock(consoleLock)
        {
            Console.SetCursorPosition(windowLocation.X, windowLocation.Y);
            Console.Write($"+{ new string('-', w*2) }+");
            for (int y = 0; y < h; y++)
            {
                Console.SetCursorPosition(windowLocation.X, windowLocation.Y + y + 1);
                Console.Write('|');
                Console.SetCursorPosition(windowLocation.X + w*2 + 1, windowLocation.Y + y + 1);
                Console.Write('|');
            }
            
            Console.SetCursorPosition(windowLocation.X, windowLocation.Y + h + 1);
            Console.Write($"+{ new string('-', w*2) }+");
        }
    }
}