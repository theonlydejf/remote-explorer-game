using System.Text.RegularExpressions;

namespace ExplorerGame.Core;

public interface IGameSession
{
    public bool IsAgentAlive { get; }
    public Tile? DiscoveredTile { get; }
    public MovementResult Move(Vector move);
}

public struct Vector
{
    public int X { get; set; }
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

    public static bool operator !=(Vector? a, Vector? b) => !(a == b);

    public static readonly Vector Zero = new (0, 0);
}

public struct Tile
{
    public char Left { get; set; }
    public char Right { get; set; }

    public Tile(char left, char right)
    {
        Left = left;
        Right = right;
    }

    public Tile(string str)
    {
        if(str.Length != 2)
            throw new ArgumentException("Tile can be constructed only from string of length 2");

        Left = str[0];
        Right = str[1];
    }

    public static implicit operator Tile(string str) => new Tile(str);

    public override string ToString() => $"{Left}{Right}";
}

public class AsyncMovementResult
{
    public bool Ready { get; set; }
    public MovementResult? MovementResult { get; set; }
    public Task ResponseHandlerTask { get; set; } = Task.CompletedTask;
}

public struct MovementResult
{
    public bool MovedSuccessfully { get; set; }
    public bool IsAgentAlive { get; set; }

    public MovementResult(bool movedSuccessfully, bool isAgentAlive)
    {
        MovedSuccessfully = movedSuccessfully;
        IsAgentAlive = isAgentAlive;
    }
}

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