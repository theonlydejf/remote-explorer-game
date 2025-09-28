using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

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
    /// <summary>
    /// A zero vector at coordinates (0, 0).
    /// </summary>
    public static readonly Vector Zero = new (0, 0);

    /// <summary>
    /// X-coordinate.
    /// </summary>
    public int X { get; set; }

    /// <summary>
    /// Y-coordinate.
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    /// Initializes a new <see cref="Vector"/> with the specified coordinates.
    /// </summary>
    /// <param name="x">The X-coordinate.</param>
    /// <param name="y">The Y-coordinate.</param>
    public Vector(int x, int y)
    {
        X = x;
        Y = y;
    }

    /// <summary>
    /// Returns a string representation of the vector in the form <c>(X, Y)</c>.
    /// </summary>
    /// <returns>A string describing this vector.</returns>
    public readonly override string ToString() => $"({X}, {Y})";

    /// <summary>
    /// Determines whether this instance and a specified object, which must also be a <see cref="Vector"/>,
    /// have the same coordinates.
    /// </summary>
    /// <param name="obj">The object to compare with the current instance.</param>
    /// <returns><c>true</c> if the objects are equal; otherwise, <c>false</c>.</returns>
    public readonly override bool Equals(object? obj)
    {
        if (obj is Vector other)
            return X == other.X && Y == other.Y;
        return false;
    }

    /// <summary>
    /// Returns a hash code for this vector.
    /// </summary>
    /// <returns>A 32-bit signed integer hash code.</returns>
    public readonly override int GetHashCode() => HashCode.Combine(X, Y);

    /// <summary>
    /// Adds two vectors component-wise.
    /// </summary>
    /// <param name="a">The first vector.</param>
    /// <param name="b">The second vector.</param>
    /// <returns>The vector sum <c>(a.X + b.X, a.Y + b.Y)</c>.</returns>
    public static Vector operator +(Vector a, Vector b) => new Vector(a.X + b.X, a.Y + b.Y);

    /// <summary>
    /// Subtracts vector <paramref name="b"/> from vector <paramref name="a"/> component-wise.
    /// </summary>
    /// <param name="a">The minuend vector.</param>
    /// <param name="b">The subtrahend vector.</param>
    /// <returns>The vector difference <c>(a.X - b.X, a.Y - b.Y)</c>.</returns>
    public static Vector operator -(Vector a, Vector b) => new Vector(a.X - b.X, a.Y - b.Y);

    /// <summary>
    /// Determines whether two nullable vectors are equal.
    /// </summary>
    /// <param name="a">The first vector, or <c>null</c>.</param>
    /// <param name="b">The second vector, or <c>null</c>.</param>
    /// <returns><c>true</c> if both are <c>null</c> or have equal coordinates; otherwise, <c>false</c>.</returns>
    public static bool operator ==(Vector? a, Vector? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        return a.Equals(b);
    }

    /// <summary>
    /// Determines whether two vectors are equal (same coordinates).
    /// </summary>
    /// <param name="a">The first vector.</param>
    /// <param name="b">The second vector.</param>
    /// <returns><c>true</c> if <paramref name="a"/> and <paramref name="b"/> are equal; otherwise, <c>false</c>.</returns>
    public static bool operator ==(Vector a, Vector b) => a.X == b.X && a.Y == b.Y;

    /// <summary>
    /// Determines whether two vectors are not equal.
    /// </summary>
    /// <param name="a">The first vector.</param>
    /// <param name="b">The second vector.</param>
    /// <returns><c>true</c> if the vectors differ; otherwise, <c>false</c>.</returns>
    public static bool operator !=(Vector a, Vector b) => !(a == b);

    /// <summary>
    /// Determines whether two nullable vectors are not equal.
    /// </summary>
    /// <param name="a">The first vector, or <c>null</c>.</param>
    /// <param name="b">The second vector, or <c>null</c>.</param>
    /// <returns><c>true</c> if they are not both <c>null</c> and not equal; otherwise, <c>false</c>.</returns>
    public static bool operator !=(Vector? a, Vector? b) => !(a == b);
}

/// <summary>
/// Represents a two-character tile on the map.
/// Each tile consists of a left and right character.
/// </summary>
public struct Tile
{
    private char left;
    private char right;

    /// <summary>
    /// Gets or sets the left character of the two-character tile.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is not an allowed character.</exception>
    public char Left
    {
        get => left;
        set
        {
            if (!IsGoodChar(value))
                throw new ArgumentException("Left character is not allowed for Tile");
            left = value;
        }
    }

    /// <summary>
    /// Gets or sets the right character of the two-character tile.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is not an allowed character.</exception>
    public char Right
    {
        get => right;
        set
        {
            if (!IsGoodChar(value))
                throw new ArgumentException("Right character is not allowed for Tile");
            right = value;
        }
    }

    /// <summary>
    /// Initializes a new <see cref="Tile"/> with the specified left and right characters.
    /// </summary>
    /// <param name="left">The left character.</param>
    /// <param name="right">The right character.</param>
    /// <exception cref="ArgumentException">Thrown when either character is not allowed.</exception>
    public Tile(char left, char right)
    {
        if (!IsGoodChar(left))
            throw new ArgumentException("Left character is not allowed for Tile");
        if (!IsGoodChar(right))
            throw new ArgumentException("Right character is not allowed for Tile");
        this.left = left;
        this.right = right;
    }

    /// <summary>
    /// Creates a tile from a 2-character string.
    /// Throws if string length is not exactly 2.
    /// </summary>
    public Tile(string str)
    {
        if (str.Length != 2)
            throw new ArgumentException("Tile can be constructed only from string of length 2");
        if (!IsGoodChar(str[0]))
            throw new ArgumentException("Left character is not allowed for Tile");
        if (!IsGoodChar(str[1]))
            throw new ArgumentException("Right character is not allowed for Tile");
        left = str[0];
        right = str[1];
    }

    /// <summary>
    /// Implicit conversion from string to tile.
    /// Allows easy construction from string literals.
    /// </summary>
    public static implicit operator Tile(string str) => new Tile(str);

    /// <summary>
    /// Returns the two-character string that represents this tile.
    /// </summary>
    /// <returns>A two-character string.</returns>
    public override string ToString() => $"{Left}{Right}";

    /// <summary>
    /// Determines whether this instance and a specified object, which must also be a <see cref="Tile"/>,
    /// have the same left and right characters.
    /// </summary>
    /// <param name="obj">The object to compare with the current instance.</param>
    /// <returns><c>true</c> if the tiles are equal; otherwise, <c>false</c>.</returns>
    public override bool Equals(object? obj)
    {
        if (obj is Tile other)
            return Left == other.Left && Right == other.Right;
        return false;
    }

    /// <summary>
    /// Returns a hash code for this tile.
    /// </summary>
    /// <returns>A 32-bit signed integer hash code.</returns>
    public override int GetHashCode() => HashCode.Combine(Left, Right);

    /// <summary>
    /// Determines whether two tiles are the same.
    /// </summary>
    /// <param name="a">The first tile.</param>
    /// <param name="b">The second tile.</param>
    /// <returns><c>true</c> if equal; otherwise, <c>false</c>.</returns>
    public static bool operator ==(Tile a, Tile b) => a.Left == b.Left && a.Right == b.Right;

    /// <summary>
    /// Determines whether two tiles differ.
    /// </summary>
    /// <param name="a">The first tile.</param>
    /// <param name="b">The second tile.</param>
    /// <returns><c>true</c> if not equal; otherwise, <c>false</c>.</returns>
    public static bool operator !=(Tile a, Tile b) => !(a == b);

    /// <summary>
    /// Serializes a nullable <see cref="Tile"/> to a <see cref="JObject"/>.
    /// </summary>
    /// <param name="tile">The tile to serialize, or <c>null</c>.</param>
    /// <returns>A <see cref="JObject"/> with the tile data, or <c>null</c> if <paramref name="tile"/> is <c>null</c>.</returns>
    public static JObject? Serialize(Tile? tile)
    {
        if (!tile.HasValue)
            return null;
        return new JObject()
        {
            ["str"] = tile.ToString()
        };
    }

    /// <summary>
    /// Deserializes a <see cref="Tile"/> from a <see cref="JObject"/>.
    /// </summary>
    /// <param name="jobj">The JSON object to read from, or <c>null</c>.</param>
    /// <returns>The deserialized tile, or <c>null</c> if <paramref name="jobj"/> is <c>null</c>.</returns>
    /// <exception cref="ArgumentException">Thrown when the JSON object does not contain the required <c>str</c> value.</exception>
    public static Tile? Deserialize(JObject? jobj)
    {
        if (jobj == null)
            return null;
        return new Tile(jobj.Value<string>("str") ?? throw new ArgumentException("Missing str in JObject"));
    }

    /// <summary>
    /// Returns true if the character is printable and not a control character or emoji.
    /// </summary>
    private static bool IsGoodChar(char c)
    {
        // Basic check: printable ASCII (space to ~)
        if (c >= 32 && c <= 126)
            return true;

        // Exclude control chars, surrogates, and private use
        if (char.IsControl(c) || char.IsSurrogate(c) || char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.PrivateUse)
            return false;

        // Exclude emoji ranges (roughly)
        int code = c;
        if ((code >= 0x1F600 && code <= 0x1F64F) || // Emoticons
            (code >= 0x1F300 && code <= 0x1F5FF) || // Misc Symbols and Pictographs
            (code >= 0x1F680 && code <= 0x1F6FF) || // Transport and Map
            (code >= 0x2600 && code <= 0x26FF) ||   // Misc symbols
            (code >= 0x2700 && code <= 0x27BF))     // Dingbats
            return false;

        // Accept other printable non-emoji Unicode
        var cat = char.GetUnicodeCategory(c);
        return cat == System.Globalization.UnicodeCategory.UppercaseLetter ||
               cat == System.Globalization.UnicodeCategory.LowercaseLetter ||
               cat == System.Globalization.UnicodeCategory.DecimalDigitNumber ||
               cat == System.Globalization.UnicodeCategory.OtherLetter ||
               cat == System.Globalization.UnicodeCategory.OtherNumber ||
               cat == System.Globalization.UnicodeCategory.MathSymbol ||
               cat == System.Globalization.UnicodeCategory.CurrencySymbol ||
               cat == System.Globalization.UnicodeCategory.ModifierSymbol ||
               cat == System.Globalization.UnicodeCategory.OtherSymbol ||
               cat == System.Globalization.UnicodeCategory.SpaceSeparator ||
               cat == System.Globalization.UnicodeCategory.DashPunctuation ||
               cat == System.Globalization.UnicodeCategory.ConnectorPunctuation ||
               cat == System.Globalization.UnicodeCategory.OtherPunctuation;
    }
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
    [MemberNotNullWhen(true, nameof(MovementResult))]
    public bool Ready { get; internal set; }

    /// <summary>
    /// The result of the movement, if available.
    /// </summary>
    public MovementResult? MovementResult { get; internal set; }

    /// <summary>
    /// Task associated with handling the response.
    /// </summary>
    public Task ResponseHandlerTask { get; internal set; } = Task.CompletedTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncMovementResult"/> class.
    /// </summary>
    /// <param name="ready"><c>true</c> if the result is already available.</param>
    /// <param name="movementResult">The movement result, if available.</param>
    /// <param name="responseHandlerTask">An optional task that handles the response workflow.</param>
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

    /// <summary>
    /// Gets or sets the tile discovered as a consequence of the movement, if any.
    /// </summary>
    public Tile? DiscoveredTile { get; set; }

    /// <summary>
    /// Initializes a new <see cref="MovementResult"/> with the specified values.
    /// </summary>
    /// <param name="movedSuccessfully"><c>true</c> if the move was valid and executed.</param>
    /// <param name="isAgentAlive"><c>true</c> if the agent is still alive after the move.</param>
    /// <param name="discoveredTile">The tile discovered by the move, if any.</param>
    public MovementResult(bool movedSuccessfully, bool isAgentAlive, Tile? discoveredTile)
    {
        MovedSuccessfully = movedSuccessfully;
        IsAgentAlive = isAgentAlive;
        DiscoveredTile = discoveredTile;
    }
}

/// <summary>
/// Represents a game session identifier, combining a server-side session ID (SID)
/// with an optional visual identifier (VSID) for rendering in the console.
/// </summary>
public class SessionIdentifier
{
    /// <summary>
    /// Server-side session ID assigned by the backend. When set, the connection is considered ready.
    /// </summary>
    public string? SID { get; set; }

    /// <summary>
    /// Optional visual identifier used for rendering (symbol and color).
    /// </summary>
    public VisualSessionIdentifier? VSID { get; set; }

    /// <summary>
    /// True when <see cref="SID"/> is present.
    /// </summary>
    public bool ConnectionReady => SID is not null;

    /// <summary>
    /// True when <see cref="VSID"/> is present.
    /// </summary>
    [MemberNotNullWhen(true, nameof(VSID))]
    [MemberNotNullWhen(true, nameof(IdentifierStr))]
    [MemberNotNullWhen(true, nameof(Color))]
    public bool HasVSID => VSID is not null;

    /// <summary>
    /// Console color associated with the visual identifier. Proxies to <see cref="VisualSessionIdentifier.Color"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when attempting to set to null.</exception>
    /// <exception cref="ArgumentException">Thrown when no <see cref="VSID"/> is associated.</exception>
    public ConsoleColor? Color
    {
        get => VSID?.Color;
        set
        {
            if (value == null)
                throw new ArgumentNullException("Property of VSID cannot be set to null");
            if (VSID == null)
                throw new ArgumentException("No VSID is associated with this SessionIdentifier instance");
            VSID.Color = value.Value;
        }
    }

    /// <summary>
    /// Two-character identifier string for rendering. Proxies to <see cref="VisualSessionIdentifier.IdentifierStr"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when attempting to set to null.</exception>
    /// <exception cref="ArgumentException">Thrown when no <see cref="VSID"/> is associated.</exception>
    public string? IdentifierStr
    {
        get => VSID?.IdentifierStr;
        set
        {
            if (value == null)
                throw new ArgumentNullException("Property of VSID cannot be set to null");
            if (VSID == null)
                throw new ArgumentException("No VSID is associated with this SessionIdentifier instance");
            VSID.IdentifierStr = value;
        }
    }

    /// <summary>
    /// Creates a session identifier backed by a new <see cref="VisualSessionIdentifier"/>.
    /// </summary>
    public SessionIdentifier(string identifier, ConsoleColor color = ConsoleColor.White, Tile?[,]? map = null)
        : this(new VisualSessionIdentifier(identifier, color, map)) { }

    /// <summary>
    /// Creates a session identifier from existing visual and server IDs.
    /// </summary>
    public SessionIdentifier(VisualSessionIdentifier? vsid = null, string? sid = null)
    {
        VSID = vsid;
        SID = sid;
    }

    /// <summary>
    /// Implicitly wraps a <see cref="VisualSessionIdentifier"/> into a <see cref="SessionIdentifier"/>.
    /// </summary>
    public static implicit operator SessionIdentifier(VisualSessionIdentifier vsid)
        => new SessionIdentifier(vsid);
}

/// <summary>
/// Visual identifier for a session (two-character symbol + console color).
/// Performs validation against reserved patterns and map tile collisions.
/// </summary>
public class VisualSessionIdentifier
{
    /// <summary>
    /// Special identifier used to indicate an error state.
    /// </summary>
    public static readonly VisualSessionIdentifier ERROR_IDENTIFIER = new() { identifierStr = "EE", color = ConsoleColor.Red };

    /// <summary>
    /// Color used when rendering a numeric counter for multiple agents on the same tile.
    /// </summary>
    public const ConsoleColor SESSION_COUNTER_COLOR = ConsoleColor.Yellow;

    private static (Regex, ConsoleColor)[]? __reserved_identifiers;

    /// <summary>
    /// Reserved identifier patterns and their colors. Prevents conflicts with internal symbols.
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

    /// <summary>
    /// Colors that are not allowed for identifiers (e.g., current console background).
    /// </summary>
    public static readonly HashSet<ConsoleColor> INVALID_COLORS = new () { Console.BackgroundColor };

    /// <summary>
    /// Returns true if the provided string/color combination is reserved.
    /// Checks reserved patterns and (optionally) collisions with map tile strings.
    /// </summary>
    /// <param name="identifierStr">Two-character identifier.</param>
    /// <param name="color">Requested console color.</param>
    /// <param name="map">Optional map to detect collisions with tile strings.</param>
    public static bool IsReserved(string identifierStr, ConsoleColor color, Tile?[,]? map = null)
    {
        // If map is provided, prevent identifiers that collide with tile strings or empty space.
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
            if (idSpec.Item2 == color && idSpec.Item1.IsMatch(identifierStr))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns true if the given identifier instance is reserved.
    /// </summary>
    public static bool IsReserved(VisualSessionIdentifier identifier, Tile?[,]? map = null)
        => IsReserved(identifier.IdentifierStr, identifier.Color, map);

    /// <summary>
    /// Returns true if the identifier string is valid (length up to 2 characters).
    /// </summary>
    private static bool IsIdentifierStringValid(string identifier)
    {
        return identifier.Length <= 2;
    }

    /// <summary>
    /// Returns true if the color choice is valid and not in <see cref="INVALID_COLORS"/>.
    /// </summary>
    private static bool IsIdentifierColorValid(ConsoleColor color)
    {
        return !INVALID_COLORS.Contains(color);
    }

    private Tile?[,]? map;

    private ConsoleColor color;

    /// <summary>
    /// Console color associated with this identifier.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when the color is not allowed or would make the identifier reserved.
    /// </exception>
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
    /// Two-character identifier string used for rendering.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when the string is longer than 2 characters or would make the identifier reserved.
    /// </exception>
    public string IdentifierStr
    {
        get => identifierStr;
        set
        {
            if (!IsIdentifierStringValid(value))
                throw new ArgumentException("Identifier string can be 2 characters at most");
            if (IsReserved(value, Color, map))
                throw new ArgumentException("This identifier is reserved");
            identifierStr = value;
        }
    }

    /// <summary>
    /// Internal constructor used for special built-in identifiers.
    /// </summary>
    internal VisualSessionIdentifier()
    {
        identifierStr = "??";
        color = ConsoleColor.White;
    }

    /// <summary>
    /// Creates a new visual session identifier and validates string and color.
    /// </summary>
    /// <param name="identifierStr">Two-character identifier string.</param>
    /// <param name="color">Console color for the identifier.</param>
    /// <param name="map">Optional map for tile-collision checks.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when the identifier string is too long, the color is invalid, or the combination is reserved.
    /// </exception>
    public VisualSessionIdentifier(string identifierStr, ConsoleColor color = ConsoleColor.White, Tile?[,]? map = null)
    {
        this.map = map;
        IdentifierStr = identifierStr;
        Color = color;
    }
}
