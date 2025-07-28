using System.Diagnostics.Contracts;
using System.Text.RegularExpressions;
using ExplorerGame.Base;
using ExplorerGame.Core;

namespace ExplorerGame.ConsoleVisualizer;

public class ConsoleVisualizer
{
    private static readonly Vector EMPTY_WINDOW_SIZE = new(4, 1);

    private Dictionary<LocalGameSession, SessionIdentifier> gameSessions = new();
    private Vector windowLocation;
    private Vector origin;
    private object consoleLock;
    private Tile?[,]? map = null;

    public ConsoleVisualizer(Vector windowLocation, object? consoleLock = null)
    {
        this.windowLocation = windowLocation;
        origin = windowLocation + new Vector(1, 1);
        if (consoleLock != null)
            this.consoleLock = consoleLock;
        else
            this.consoleLock = new object();

        PrintBorder();
        PrintNoMapMsg();
    }

    public bool AttachGameSession(LocalGameSession gameSession, SessionIdentifier identifier) => AttachGameSession(gameSession, identifier, Vector.Zero);

    public bool AttachGameSession(LocalGameSession gameSession, SessionIdentifier identifier, Vector agentLocation)
    {
        if (map != null && !map.Equals(gameSession.map))
            throw new ArgumentException("Trying to attach two game sessions with different maps onto one visualizer");

        if (gameSessions.ContainsKey(gameSession))
            return false;

        gameSession.AgentDied += OnAgentDied;
        gameSession.AgentMoved += OnAgentMoved;

        gameSessions.Add(gameSession, identifier);
        if (map == null)
        {
            map = gameSession.map;
            PrintBorder();
            UpdateWindow();
        }
        else
            UpdatePixel(gameSession.AgentLocation);
        return true;
    }

    public void AttachMap(Tile?[,] map)
    {
        if (this.map != null)
            throw new InvalidOperationException("Map can be attached only once");

        this.map = map;
        PrintBorder();
        UpdateWindow();
    }

    private void OnAgentDied(object? sender, EventArgs e)
    {
        if (sender == null)
            return; // Ignore unknown senders

        if (sender is LocalGameSession session)
        {
            gameSessions.Remove(session);
            UpdatePixel(session.AgentLocation);
        }
    }

    private void OnAgentMoved(object? sender, AgentMovementEventArgs e)
    {
        if (sender == null)
            return; // Ignore unknown senders
        UpdatePixel(e.AgentPreviousLocation);
        UpdatePixel(e.AgentCurrentLocation);
    }

    private void UpdateWindow()
    {
        if (map == null)
        {
            PrintNoMapMsg();
            return;
        }

        int w = map.GetLength(0);
        int h = map.GetLength(1);

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                UpdatePixel(new(x, y));
    }

    private void UpdatePixel(Vector loc)
    {
        if (map == null)
        {
            PrintNoMapMsg();
            return;
        }

        if (loc.X < 0 || loc.X >= map.GetLength(0) ||
            loc.Y < 0 || loc.Y >= map.GetLength(1))
        {
            return;
        }

        int agentCnt = 0;
        SessionIdentifier id = SessionIdentifier.ERROR_IDENTIFIER;
        foreach (var keyVal in gameSessions)
        {
            if (keyVal.Key.AgentLocation == loc)
            {
                agentCnt++;
                id = keyVal.Value;
            }
        }
        lock (consoleLock)
        {
            Console.SetCursorPosition(origin.X + loc.X * 2, origin.Y + loc.Y);
            if (agentCnt > 1)
            {
                Console.ForegroundColor = SessionIdentifier.SESSION_COUNTER_COLOR;
                if (agentCnt > 99)
                    Console.Write("Hi");
                else
                    Console.Write(agentCnt.ToString().PadRight(2, ' '));
                return;
            }

            if (agentCnt == 1)
            {
                Console.ForegroundColor = id.Color;
                Console.Write(id.Identifier);
                return;
            }

            Console.ResetColor();
            if (map[loc.X, loc.Y].HasValue)
                Console.Write(map[loc.X, loc.Y].ToString());
            else
                Console.Write("  ");
        }
    }

    private void PrintBorder()
    {
        int w = map?.GetLength(0) ?? EMPTY_WINDOW_SIZE.X;
        int h = map?.GetLength(1) ?? EMPTY_WINDOW_SIZE.Y;

        lock (consoleLock)
        {
            Console.SetCursorPosition(windowLocation.X, windowLocation.Y);
            Console.Write($"┌{new string('─', w * 2)}┐");
            for (int y = 0; y < h; y++)
            {
                Console.SetCursorPosition(windowLocation.X, windowLocation.Y + y + 1);
                Console.Write('│');
                Console.SetCursorPosition(windowLocation.X + w * 2 + 1, windowLocation.Y + y + 1);
                Console.Write('│');
            }

            Console.SetCursorPosition(windowLocation.X, windowLocation.Y + h + 1);
            Console.Write($"└{new string('─', w * 2)}┘");
        }
    }

    private void PrintNoMapMsg()
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.SetCursorPosition(origin.X, origin.Y);
        Console.Write(" No map");
        Console.ResetColor();
    }
}