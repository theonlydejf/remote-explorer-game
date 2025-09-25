using System.Collections;
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
        if (str.Length != 2)
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

    public override bool Equals(object? obj)
    {
        if (obj is Tile other)
            return Left == other.Left && Right == other.Right;
        return false;
    }

    public override int GetHashCode() => HashCode.Combine(Left, Right);

    public static bool operator ==(Tile a, Tile b) => a.Left == b.Left && a.Right == b.Right;
    public static bool operator !=(Tile a, Tile b) => !(a == b);

    public static JObject? Serialize(Tile? tile)
    {
        if (!tile.HasValue)
            return null;
        return new JObject()
        {
            ["str"] = tile.ToString()
        };
    }

    public static Tile? Deserialize(JObject? jobj)
    {
        if (jobj == null)
            return null;
        return new Tile(jobj.Value<string>("str") ?? throw new ArgumentException("Missing str in JObject"));
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

    public Tile? DiscoveredTile { get; set; }

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
    public SessionIdentifier(string identifier, ConsoleColor color = ConsoleColor.White, Map? map = null)
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
    public static bool IsReserved(string identifierStr, ConsoleColor color, Map? map = null)
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
    public static bool IsReserved(VisualSessionIdentifier identifier, Map? map = null)
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

    private Map? map;

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
    public VisualSessionIdentifier(string identifierStr, ConsoleColor color = ConsoleColor.White, Map? map = null)
    {
        this.map = map;
        IdentifierStr = identifierStr;
        Color = color;
    }
}

/// <summary>
/// Represents a two-dimensional map composed of <see cref="Tile"/> entries.
/// Each cell may contain a tile or be empty (<c>null</c>).
/// Coordinates use row-major storage: <c>tiles[y, x]</c>.
/// </summary>
public class Map : IEnumerable<Tile?>
{
    private readonly Tile?[,] tiles;

    /// <summary>
    /// Gets the width of the map (number of columns).
    /// </summary>
    public int Width => tiles.GetLength(1);

    /// <summary>
    /// Gets the height of the map (number of rows).
    /// </summary>
    public int Height => tiles.GetLength(0);

    /// <summary>
    /// Creates a new empty map with the specified width and height.
    /// </summary>
    /// <param name="w">Width of the map (number of columns).</param>
    /// <param name="h">Height of the map (number of rows).</param>
    public Map(int w, int h)
    {
        tiles = new Tile?[w, h]; // storage is [rows, columns] = [y, x]
    }

    /// <summary>
    /// Creates a map from an existing two-dimensional tile array.
    /// </summary>
    /// <param name="tiles">2D array of tiles (may contain null entries).</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="tiles"/> is null.</exception>
    public Map(Tile?[,] tiles)
    {
        this.tiles = tiles ?? throw new ArgumentNullException(nameof(tiles));
    }

    /// <summary>
    /// Gets or sets the tile at the given row and column.
    /// </summary>
    /// <param name="x">Column index.</param>
    /// <param name="y">Row index.</param>
    /// <returns>The tile at the specified location, or <c>null</c> if empty.</returns>
    public Tile? this[int x, int y]
    {
        get => tiles[x, y];
        set => tiles[x, y] = value;
    }

    /// <summary>
    /// Gets or sets the tile at the given position using a <see cref="Vector"/>.
    /// Note: <see cref="Vector.X"/> is the column index and <see cref="Vector.Y"/> is the row index.
    /// </summary>
    /// <param name="v">Vector representing the coordinates (X = column, Y = row).</param>
    /// <returns>The tile at the specified location, or <c>null</c> if empty.</returns>
    public Tile? this[Vector v]
    {
        get => this[v.X, v.Y];
        set => this[v.X, v.Y] = value;
    }

    /// <summary>
    /// Determines whether the given row and column indices are within map bounds.
    /// </summary>
    /// <param name="x">Column index to check.</param>
    /// <param name="y">Row index to check.</param>
    /// <returns><c>true</c> if the indices are valid; otherwise <c>false</c>.</returns>
    public bool IsInBounds(int x, int y)
    {
        return x >= 0 && x < Width && y >= 0 && y < Height;
    }

    /// <summary>
    /// Determines whether the given vector position is within map bounds.
    /// </summary>
    /// <param name="v">Vector representing the coordinates (X = column, Y = row).</param>
    /// <returns><c>true</c> if the position is valid; otherwise <c>false</c>.</returns>
    public bool IsInBounds(Vector v) => IsInBounds(v.X, v.Y);

    /// <summary>
    /// Returns true if the given coordinates do not contain a tile.
    /// </summary>
    /// <param name="x">Column index.</param>
    /// <param name="y">Row index.</param>
    /// <returns><c>true</c> if the position is empty; otherwise <c>false</c>.</returns>
    public bool IsSafe(int x, int y) => !this[x, y].HasValue;

    /// <summary>
    /// Returns true if the given vector position does not contain a tile.
    /// </summary>
    /// <param name="v">Vector representing the coordinates (X = column, Y = row).</param>
    /// <returns><c>true</c> if the position is empty; otherwise <c>false</c>.</returns>
    public bool IsSafe(Vector v) => IsSafe(v.X, v.Y);

    /// <summary>
    /// Returns an enumerator that iterates through all tiles in row-major order.
    /// </summary>
    public IEnumerator<Tile?> GetEnumerator()
    {
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                yield return this[x, y];
    }

    /// <summary>
    /// Returns an enumerator that iterates through all tiles in row-major order.
    /// </summary>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Determines whether the specified object is equal to the current map.
    /// Two maps are equal if they have the same dimensions and identical tile values.
    /// </summary>
    /// <param name="obj">The object to compare with the current map.</param>
    /// <returns><c>true</c> if the maps are equal; otherwise <c>false</c>.</returns>
    public override bool Equals(object? obj)
    {
        if (obj is not Map other)
            return false;

        if (Width != other.Width || Height != other.Height)
            return false;

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (!Equals(this[x, y], other[x, y]))
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Returns a hash code for the map.
    /// Combines dimensions and tile values to produce a hash.
    /// </summary>
    public override int GetHashCode()
    {
        int hash = HashCode.Combine(Width, Height);
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                hash = HashCode.Combine(hash, this[x, y]);
            }
        }
        return hash;
    }
}
