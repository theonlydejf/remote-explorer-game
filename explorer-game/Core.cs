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

public class SessionIdentifier
{
    public string? SID { get; set; }

    public VisualSessionIdentifier? VSID { get; set; }

    public bool ConnectionReady => SID != null;
    public bool HasVSID => VSID != null;

    /// <summary>
    /// Console color associated with this identifier.
    /// </summary>
    public ConsoleColor? Color
    {
        get => VSID?.Color;
        set
        {
            if (value == null)
                throw new ArgumentNullException("Property of VSID cannot be set to null");
            if (VSID == null)
                throw new ArgumentException("No VSID is associated with this SID instance");
            VSID.Color = value.Value;
        }
    }

    /// <summary>
    /// Two-character identifier string.
    /// </summary>
    public string? IdentifierStr
    {
        get => VSID?.IdentifierStr;
        set
        {
            if (value == null)
                throw new ArgumentNullException("Property of VSID cannot be set to null");
            if (VSID == null)
                throw new ArgumentException("No VSID is associated with this SID instance");
            VSID.IdentifierStr = value;
        }
    }

    public SessionIdentifier(string identifier, ConsoleColor color = ConsoleColor.White, Tile?[,]? map = null)
        : this(new VisualSessionIdentifier(identifier, color, map)) { }

    public SessionIdentifier(VisualSessionIdentifier? vsid = null, string? sid = null)
    {
        VSID = vsid;
        SID = sid;
    }

    public static implicit operator SessionIdentifier(VisualSessionIdentifier vsid)
        => new SessionIdentifier(vsid);
}

/// <summary>
/// Represents an identifier for a session, including a text symbol
/// and a console color. Ensures that identifiers are unique and not reserved.
/// </summary>
public class VisualSessionIdentifier
{
    /// <summary>
    /// Special identifier used to indicate an error.
    /// </summary>
    public static readonly VisualSessionIdentifier ERROR_IDENTIFIER = new() { identifierStr = "EE", color = ConsoleColor.Red };

    /// <summary>
    /// Color used when displaying the number of multiple agents on a tile.
    /// </summary>
    public const ConsoleColor SESSION_COUNTER_COLOR = ConsoleColor.Yellow;

    private static (Regex, ConsoleColor)[]? __reserved_identifiers;
    /// <summary>
    /// Reserved identifiers and their associated colors.
    /// Prevents conflicts with internal symbols like error markers
    /// or session counters.
    /// </summary>
    private static (Regex, ConsoleColor)[] RESERVED_IDENTIFIERS
    {
        get
        {
            if (__reserved_identifiers == null)
            {
                __reserved_identifiers = new (Regex, ConsoleColor)[]
                {
                    (new Regex(ERROR_IDENTIFIER.IdentifierStr), ERROR_IDENTIFIER.Color),
                    (new Regex(@"\d+|Hi"), SESSION_COUNTER_COLOR)
                };
            }
            return __reserved_identifiers;
        }
    }

    public static readonly HashSet<ConsoleColor> INVALID_COLORS = new () { Console.BackgroundColor };

    /// <summary>
    /// Determines if a session identifier is reserved.
    /// Checks against internal reserved patterns and map tile strings.
    /// </summary>
    public static bool IsReserved(string identifierStr, ConsoleColor color, Tile?[,]? map = null)
    {
        // If map is provided, prevent identifiers that collide with tile strings.
        if (map != null && color == ConsoleColor.White)
        {
            foreach (Tile? tile in map)
            {
                if (!tile.HasValue)
                {
                    if (identifierStr == "  ")
                        return true;
                    continue;
                }
                if (tile.ToString() == identifierStr)
                        return true;
            }
        }

        // Check regex-based reserved identifiers.
        foreach (var idSpec in RESERVED_IDENTIFIERS)
        {
            if (idSpec.Item2 == color &&
                idSpec.Item1.IsMatch(identifierStr))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Determines if a session identifier is reserved.
    /// Checks against internal reserved patterns and map tile strings.
    /// </summary>
    public static bool IsReserved(VisualSessionIdentifier identifier, Tile?[,]? map = null)
        => IsReserved(identifier.IdentifierStr, identifier.Color, map);

    private static bool IsIdentifierStringValid(string identifier)
    {
        return identifier.Length <= 2;
    }

    private static bool IsIdentifierColorValid(ConsoleColor color)
    {
        return !INVALID_COLORS.Contains(color);
    }

    private Tile?[,]? map;

    private ConsoleColor color;
    /// <summary>
    /// Console color associated with this identifier.
    /// </summary>
    public ConsoleColor Color
    {
        get => color;
        set
        {
            if (!IsIdentifierColorValid(value))
                throw new ArgumentException("Invalid identifier color");
            if (IsReserved(IdentifierStr, value, map))
                throw new ArgumentException("This identifier is reserved");

            color = value;
        }
    }

    private string identifierStr = "??";
    /// <summary>
    /// Two-character identifier string.
    /// </summary>
    public string IdentifierStr
    {
        get => identifierStr;
        set
        {
            if (!IsIdentifierStringValid(value))
                throw new ArgumentException("Identifier string can be 2 bytes at most");
            if (IsReserved(value, Color, map))
                throw new ArgumentException("This identifier is reserved");
            identifierStr = value;
        }
    }

    /// <summary>
    /// Internal constructor used for error and reserved identifiers.
    /// </summary>
    internal VisualSessionIdentifier()
    {
        identifierStr = "??";
        color = ConsoleColor.White;
    }

    /// <summary>
    /// Creates a new session identifier with validation.
    /// </summary>
    /// <param name="identifier">Two-character identifier string.</param>
    /// <param name="color">Console color for the identifier.</param>
    /// <param name="map"> /// Optional map for tile collision checks.  /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown if identifier is too long, uses black color, or is reserved.
    /// </exception>
    public VisualSessionIdentifier(string identifierStr, ConsoleColor color = ConsoleColor.White, Tile?[,]? map = null)
    {
        this.map = map;
        IdentifierStr = identifierStr;
        Color = color;
    }
}
