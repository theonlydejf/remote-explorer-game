using System.Text.RegularExpressions;

namespace ExplorerGame.Core;

/// <summary>
/// Common interface for any game session implementation.
/// Defines the essential properties and actions related to an agent.
/// </summary>
public interface IGameSession
{
    /// <summary>
    /// Indicates whether the agent is still alive.
    /// </summary>
    public bool IsAgentAlive { get; }

    /// <summary>
    /// Tile that caused the agent's death, if applicable.
    /// </summary>
    public Tile? DiscoveredTile { get; }

    /// <summary>
    /// Attempts to move the agent by the given vector.
    /// Returns the result of the movement attempt.
    /// </summary>
    public MovementResult Move(Vector move);
}

/// <summary>
/// Represents a 2D coordinate or movement vector.
/// </summary>
public struct Vector
{
    public static readonly Vector Zero = new (0, 0);

    /// <summary>
    /// X-coordinate.
    /// </summary>
    public int X { get; set; }

    /// <summary>
    /// Y-coordinate.
    /// </summary>
    public int Y { get; set; }

    public Vector(int x, int y)
    {
        X = x;
        Y = y;
    }

    public readonly override string ToString() => $"({X}, {Y})";

    public readonly override bool Equals(object? obj)
    {
        if (obj is Vector other)
            return X == other.X && Y == other.Y;
        return false;
    }

    public readonly override int GetHashCode() => HashCode.Combine(X, Y);

    public static Vector operator +(Vector a, Vector b) => new Vector(a.X + b.X, a.Y + b.Y);
    public static Vector operator -(Vector a, Vector b) => new Vector(a.X - b.X, a.Y - b.Y);

    public static bool operator ==(Vector? a, Vector? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        return a.Equals(b);
    }

    public static bool operator ==(Vector a, Vector b) => a.X == b.X && a.Y == b.Y;
    public static bool operator !=(Vector a, Vector b) => !(a == b);
    public static bool operator !=(Vector? a, Vector? b) => !(a == b);
}

/// <summary>
/// Represents a two-character tile on the map.
/// Each tile consists of a left and right character.
/// </summary>
public struct Tile
{
    public char Left { get; set; }
    public char Right { get; set; }

    public Tile(char left, char right)
    {
        Left = left;
        Right = right;
    }

    /// <summary>
    /// Creates a tile from a 2-character string.
    /// Throws if string length is not exactly 2.
    /// </summary>
    public Tile(string str)
    {
        if(str.Length != 2)
            throw new ArgumentException("Tile can be constructed only from string of length 2");

        Left = str[0];
        Right = str[1];
    }

    /// <summary>
    /// Implicit conversion from string to tile.
    /// Allows easy construction from string literals.
    /// </summary>
    public static implicit operator Tile(string str) => new Tile(str);

    public override string ToString() => $"{Left}{Right}";
}

/// <summary>
/// Represents an asynchronous result of a movement attempt.
/// Contains the movement result once ready, plus a task to
/// handle the response.
/// </summary>
public class AsyncMovementResult
{
    /// <summary>
    /// Indicates whether the result is ready.
    /// </summary>
    public bool Ready { get; internal set; }

    /// <summary>
    /// The result of the movement, if available.
    /// </summary>
    public MovementResult? MovementResult { get; internal set; }

    /// <summary>
    /// Task associated with handling the response.
    /// </summary>
    public Task ResponseHandlerTask { get; internal set; } = Task.CompletedTask;

    public AsyncMovementResult(bool ready = false, MovementResult? movementResult = null, Task? responseHandlerTask = null)
    {
        Ready = ready;
        MovementResult = movementResult;
        ResponseHandlerTask = responseHandlerTask ?? Task.CompletedTask;
    }
}

/// <summary>
/// Represents the outcome of a movement attempt.
/// Contains both success and survival information.
/// </summary>
public struct MovementResult
{
    /// <summary>
    /// Indicates whether the move was valid and executed.
    /// </summary>
    public bool MovedSuccessfully { get; set; }

    /// <summary>
    /// Indicates whether the agent is still alive after the move.
    /// </summary>
    public bool IsAgentAlive { get; set; }

    public MovementResult(bool movedSuccessfully, bool isAgentAlive)
    {
        MovedSuccessfully = movedSuccessfully;
        IsAgentAlive = isAgentAlive;
    }
}

/// <summary>
/// Represents an identifier for a session, including a text symbol
/// and a console color. Ensures that identifiers are unique and not reserved.
/// </summary>
public class SessionIdentifier
{
    /// <summary>
    /// Special identifier used to indicate an error.
    /// </summary>
    public static readonly SessionIdentifier ERROR_IDENTIFIER = new() { Identifier = "EE", Color = ConsoleColor.Red};

    /// <summary>
    /// Color used when displaying the number of multiple agents on a tile.
    /// </summary>
    public const ConsoleColor SESSION_COUNTER_COLOR = ConsoleColor.Yellow;

    private static (Regex, ConsoleColor)[]? __RESERVED_IDENTIFIERS;

    /// <summary>
    /// Reserved identifiers and their associated colors.
    /// Prevents conflicts with internal symbols like error markers
    /// or session counters.
    /// </summary>
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

    /// <summary>
    /// Determines if a session identifier is reserved.
    /// Checks against internal reserved patterns and map tile strings.
    /// </summary>
    public static bool IsReserved(SessionIdentifier identifier, Tile?[,]? map = null)
    {
        // If map is provided, prevent identifiers that collide with tile strings.
        if(map != null && identifier.Color == ConsoleColor.White)
        {
            foreach(Tile? tile in map)
            {
                if(tile.ToString() == identifier.Identifier)
                    return true;
            }
        }

        // Check regex-based reserved identifiers.
        foreach(var idSpec in RESERVED_IDENTIFIERS)
        {
            if(idSpec.Item2 == identifier.Color &&
                idSpec.Item1.IsMatch(identifier.Identifier))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Console color associated with this identifier.
    /// </summary>
    public ConsoleColor Color { get; set; }

    /// <summary>
    /// Two-character identifier string.
    /// </summary>
    public string Identifier { get; set; }

    /// <summary>
    /// Internal constructor used for error and reserved identifiers.
    /// </summary>
    internal SessionIdentifier()
    {
        Identifier = "??";
        Color = ConsoleColor.White;
    }

    /// <summary>
    /// Creates a new session identifier with validation.
    /// </summary>
    /// <param name="identifier">Two-character identifier string.</param>
    /// <param name="color">Console color for the identifier.</param>
    /// <param name="map">
    /// Optional map for tile collision checks.
    /// The map won't be associated with this identifier.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown if identifier is too long, uses black color, or is reserved.
    /// </exception>
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
