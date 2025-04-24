namespace ExplorerGame.Core;

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
    public char Pixel { get; set; }

    public Tile(char pixel)
    {
        Pixel = pixel;
    }
}

public struct MovementResult
{
    public bool MovedSuccessfully { get; set; }
    public bool IsPlayerAlive { get; set; }

    public MovementResult(bool movedSuccessfully, bool isPlayerAlive)
    {
        MovedSuccessfully = movedSuccessfully;
        IsPlayerAlive = isPlayerAlive;
    }
}