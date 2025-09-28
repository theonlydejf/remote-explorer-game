using ExplorerGame.Core;

enum CellState
{
    Undiscovered,
    Safe,
    Trap,
    Unreachable,
    Reserved
}

static class CellStateInfo
{
    public static (string Symbol, ConsoleColor Color) GetDisplay(CellState state)
    {
        return state switch
        {
            CellState.Undiscovered => ("**", ConsoleColor.DarkGray),
            CellState.Safe         => ("..", ConsoleColor.White),
            CellState.Trap         => ("XX", ConsoleColor.Red),
            CellState.Unreachable  => ("??", ConsoleColor.Yellow),
            CellState.Reserved     => ("**", ConsoleColor.Magenta),
            _                      => ("??", ConsoleColor.DarkRed)
        };
    }
}

/// <summary>
/// Represents a 2D grid of cells for the game world, tracking each cell’s state (undiscovered,
/// safe, trap, unreachable, or reserved). Supports dynamic resizing, cell state updates,
/// and efficient searching for undiscovered cells. Provides methods to grow the map,access
/// cells, and select the closest or a random undiscovered cell for exploration.
/// </summary>
class Map
{
    private CellState[,] map;
    
    public Map(int width, int height)
    {
        map = new CellState[width, height];
        UndiscoveredCnt = width * height;
    }

    public int Width => map.GetLength(0);
    public int Height => map.GetLength(1);

    /// <summary>
    /// How many undiscovered cells are in the map
    /// </summary>
    public int UndiscoveredCnt { get; private set; }

    public CellState this[Vector vec]
    {
        get => this[vec.X, vec.Y];
        set => this[vec.X, vec.Y] = value;
    }

    public CellState this[int row, int col]
    {
        get
        {
            if (row < 0 || col < 0)
                return CellState.Trap; // Too far left -> always deadly

            if (row >= Width || col >= Height)
                return CellState.Undiscovered; // Too far right -> map can grow this way, so maybe something?

            return map[row, col];
        }
        set
        {
            if (row < 0 || col < 0 || row >= Width || col >= Height)
                return; // ignore out of bounds write

            if (map[row, col] == CellState.Undiscovered && value != CellState.Undiscovered)
                UndiscoveredCnt--;
            if (map[row, col] != CellState.Undiscovered && value == CellState.Undiscovered)
                UndiscoveredCnt++;

            map[row, col] = value;
        }
    }

    /// <summary>
    /// Expands the map by increasing its width and/or height. Newly added cells are set to Undiscovered.
    /// Previously Unreachable cells are reset to Undiscovered, allowing further exploration.
    /// Updates the count of undiscovered cells.
    /// </summary>
    /// <param name="widthIncrease">Columns to add</param>
    /// <param name="heightIncrease">Rows to add</param>
    public void Grow(int widthIncrease = 1, int heightIncrease = 1)
    {
        int newWidth = Width + widthIncrease;
        int newHeight = Height + heightIncrease;
        var newMap = new CellState[newWidth, newHeight];

        // Copy old map contents to new map
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (map[x, y] == CellState.Unreachable) // Maybe a new route to unreachable cells?
                {
                    newMap[x, y] = CellState.Undiscovered;
                    UndiscoveredCnt++;
                }
                else
                    newMap[x, y] = map[x, y];
            }
        }

        // Count new undiscovered cells
        UndiscoveredCnt += (newWidth * newHeight) - (Width * Height);

        map = newMap;
    }

    /// <summary>
    /// Finds the undiscovered cell closest to the given point using BFS
    /// </summary>
    /// <param name="point">The starting position for the search</param>
    /// <returns>The closest undiscovered cell, or null if all cells are discovered.</returns>
    public Vector? PickClosestUndiscovered(Vector point)
    {
        var visited = new bool[Width, Height];
        var queue = new Queue<Vector>();
        queue.Enqueue(point);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            int x = current.X, y = current.Y;

            if (x < 0 || y < 0 || x >= Width || y >= Height || visited[x, y])
                continue;

            visited[x, y] = true;

            if (map[x, y] == CellState.Undiscovered)
                return current;

            // Add neighbors (up, down, left, right)
            queue.Enqueue(new Vector(x + 1, y));
            queue.Enqueue(new Vector(x - 1, y));
            queue.Enqueue(new Vector(x, y + 1));
            queue.Enqueue(new Vector(x, y - 1));
        }
        return null;
    }

    /// <summary>
    /// Picks a random undiscovered cell in the map using the provided random number generator.
    /// Returns null if there are no undiscovered cells.
    /// </summary>
    /// <param name="rng">Random number generator to use for selection.</param>
    /// <returns>A random undiscovered cell, or null if none exist</returns>
    public Vector? PickRandomUndiscovered(Random rng)
    {
        if (UndiscoveredCnt <= 0)
            return null;

        int target = rng.Next(UndiscoveredCnt);
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (map[x, y] == CellState.Undiscovered)
                {
                    if (target == 0)
                        return new Vector(x, y);
                    target--;
                }
            }
        }
        return null;
    }
}
