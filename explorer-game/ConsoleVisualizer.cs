using ExplorerGame.Base;
using ExplorerGame.Core;

namespace ExplorerGame.ConsoleVisualizer;

/// <summary>
/// Provides a console-based visualization of a map and the agents (sessions) moving around it.
/// Renders a bordered "window" in the console, updates tiles as agents move/die,
/// and handles multiple sessions being displayed simultaneously.
/// </summary>
public class ConsoleVisualizer
{
    /// <summary>
    /// Fallback size of the console window when no map is yet attached.
    /// (4x1 = enough space to draw a small placeholder border and "No map" message.)
    /// </summary>
    private static readonly Vector EMPTY_WINDOW_SIZE = new(4, 1);

    /// <summary>
    /// Maps each game session to its identifier for display (color + symbol).
    /// </summary>
    private Dictionary<LocalGameSession, VisualSessionIdentifier> gameSessions = new();

    /// <summary>
    /// Top-left coordinate of the visualization window in the console.
    /// </summary>
    private Vector windowLocation;
    public Vector WindowLocation
    {
        get => windowLocation;
        set
        {
            if (windowLocation == value)
                return;

            ClearArea();
            windowLocation = value;
            PrintBorder();
            UpdateWindow();
        }
    }

    /// <summary>
    /// Offseted top-left coordinate inside the border where actual map content begins.
    /// (Because of drawn border, origin is windowLocation + (1,1).)
    /// </summary>
    private Vector origin => WindowLocation + new Vector(1, 1);

    /// <summary>
    /// Synchronization lock for writing to the console.
    /// </summary>
    private object consoleLock;

    /// <summary>
    /// Current map being displayed. Nullable until explicitly attached.
    /// </summary>
    private Tile?[,]? map = null;

    /// <summary>
    /// Creates a new console visualizer at a given window location.
    /// Initializes with either a provided console lock (to synchronize across multiple visualizers),
    /// or creates its own lock.
    /// </summary>
    public ConsoleVisualizer(Vector windowLocation, object? consoleLock = null)
    {
        this.windowLocation = windowLocation;
        this.consoleLock = consoleLock ?? new object();

        // Draw border immediately so the user sees a placeholder box,
        // then show a "No map" message if no map attached yet.
        PrintBorder();
        PrintNoMapMsg();
    }

    /// <summary>
    /// Convenience overload for attaching a game session at the default agent location (0,0).
    /// </summary>
    public bool AttachGameSession(LocalGameSession gameSession, VisualSessionIdentifier identifier) =>
        AttachGameSession(gameSession, identifier, Vector.Zero);

    /// <summary>
    /// Attaches a game session with its display identifier, tracking the agent's location.
    /// </summary>
    public bool AttachGameSession(LocalGameSession gameSession, VisualSessionIdentifier identifier, Vector agentLocation)
    {
        // A visualizer is designed for exactly one map layout.
        if (map != null && !map.Equals(gameSession.map))
            throw new ArgumentException("Trying to attach two game sessions with different maps onto one visualizer");

        // Prevent duplicate attachment of the same session.
        if (gameSessions.ContainsKey(gameSession))
            return false;

        // Hook into session events so console can update in real time.
        gameSession.AgentDied += OnAgentDied;
        gameSession.AgentMoved += OnAgentMoved;

        gameSessions.Add(gameSession, identifier);

        if (map == null)
        {
            // If first session being attached, adopt its map as the one to render.
            map = gameSession.map;
            PrintBorder();
            UpdateWindow(); // Draw entire map
        }
        else
        {
            // Otherwise, just draw the tile at the agent’s location.
            UpdatePixel(gameSession.AgentLocation);
        }

        return true;
    }

    /// <summary>
    /// Manually attaches a map without attaching a session.
    /// Can only be called once, since visualizer is bound to a single map.
    /// </summary>
    public void AttachMap(Tile?[,] map)
    {
        if (this.map != null)
            throw new InvalidOperationException("Map can be attached only once");

        this.map = map;
        PrintBorder();
        UpdateWindow();
    }

    /// <summary>
    /// Handler for when an agent dies: removes it and refreshes its last tile.
    /// </summary>
    private void OnAgentDied(object? sender, EventArgs e)
    {
        if (sender == null)
            return; // Ignore unexpected sender types

        if (sender is LocalGameSession session)
        {
            gameSessions.Remove(session);
            UpdatePixel(session.AgentLocation);
        }
    }

    /// <summary>
    /// Handler for when an agent moves: refresh old location and new location.
    /// </summary>
    private void OnAgentMoved(object? sender, AgentMovementEventArgs e)
    {
        if (sender == null)
            return;

        UpdatePixel(e.AgentPreviousLocation);
        UpdatePixel(e.AgentCurrentLocation);
    }

    /// <summary>
    /// Redraws the entire window (every map tile).
    /// Useful when the map is first attached or border has been reprinted.
    /// </summary>
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

    /// <summary>
    /// Redraws a single tile at a given location.
    /// Decides whether to show agents, agent counters, map tile, or empty space.
    /// </summary>
    private void UpdatePixel(Vector loc)
    {
        if (map == null)
        {
            PrintNoMapMsg();
            return;
        }

        // Skip invalid coordinates (e.g. if agent moves outside map).
        if (loc.X < 0 || loc.X >= map.GetLength(0) ||
            loc.Y < 0 || loc.Y >= map.GetLength(1))
        {
            return;
        }

        // Count how many agents occupy this tile.
        int agentCnt = 0;
        VisualSessionIdentifier id = VisualSessionIdentifier.ERROR_IDENTIFIER;
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
                // Multiple agents in one tile -> display their count instead of identifiers.
                Console.ForegroundColor = VisualSessionIdentifier.SESSION_COUNTER_COLOR;

                // If absurdly many agents, fallback to "Hi" as a placeholder.
                if (agentCnt > 99)
                    Console.Write("Hi");
                else
                    Console.Write(agentCnt.ToString().PadRight(2, ' '));
                return;
            }

            if (agentCnt == 1)
            {
                // One agent -> show its identifier in its assigned color.
                Console.ForegroundColor = id.Color;
                Console.Write(id.IdentifierStr);
                return;
            }

            // No agents -> restore underlying map tile or empty space.
            Console.ResetColor();
            if (map[loc.X, loc.Y].HasValue)
                Console.Write(map[loc.X, loc.Y].ToString());
            else
                Console.Write("  "); // Two spaces so layout remains aligned
        }
    }

    /// <summary>
    /// Prints a rectangular border around the visualization area.
    /// Adapts size based on whether a map is already present.
    /// </summary>
    private void PrintBorder()
    {
        int w = map?.GetLength(0) ?? EMPTY_WINDOW_SIZE.X;
        int h = map?.GetLength(1) ?? EMPTY_WINDOW_SIZE.Y;

        lock (consoleLock)
        {
            Console.ResetColor();

            // Top border
            Console.SetCursorPosition(WindowLocation.X, WindowLocation.Y);
            Console.Write($"┌{new string('─', w * 2)}┐");

            // Vertical sides
            for (int y = 0; y < h; y++)
            {
                Console.SetCursorPosition(WindowLocation.X, WindowLocation.Y + y + 1);
                Console.Write('│');
                Console.SetCursorPosition(WindowLocation.X + w * 2 + 1, WindowLocation.Y + y + 1);
                Console.Write('│');
            }

            // Bottom border
            Console.SetCursorPosition(WindowLocation.X, WindowLocation.Y + h + 1);
            Console.Write($"└{new string('─', w * 2)}┘");
        }
    }

    /// <summary>
    /// Prints a placeholder "No map" message inside the border when no map is loaded.
    /// </summary>
    private void PrintNoMapMsg()
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.SetCursorPosition(origin.X, origin.Y);
        Console.Write(" No map");
        Console.ResetColor();
    }

    /// <summary>
    /// Clears the visualization area inside the border (map area).
    /// Useful for resetting the display before redrawing.
    /// </summary>
    private void ClearArea()
    {
        int w = (map?.GetLength(0) * 2 ?? EMPTY_WINDOW_SIZE.X) + 2;
        int h = (map?.GetLength(1) ?? EMPTY_WINDOW_SIZE.Y) + 2;

        lock (consoleLock)
        {
            Console.ResetColor();
            for (int y = 0; y < h; y++)
            {
                Console.SetCursorPosition(WindowLocation.X, WindowLocation.Y + y);
                Console.Write(new string(' ', w));
            }
        }
    }
}
