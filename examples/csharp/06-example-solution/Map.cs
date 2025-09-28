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

// Put in a new file or above your classes
static class NeighborUtils
{
    /// <summary>
    /// Generates valid neighbor positions from 'pos', respecting map bounds
    /// and the "allowJumps" rule. Uses exactly the same movement model as Agent.
    /// </summary>
    public static IEnumerable<Vector> GetNeighbors(Map map, Vector pos, bool allowJumps)
    {
        // 4-neighborhood with optional length-2 jumps
        Vector[] steps = allowJumps
            ? new Vector[] {
                new(pos.X+1, pos.Y), new(pos.X-1, pos.Y), new(pos.X, pos.Y+1), new(pos.X, pos.Y-1),
                new(pos.X+2, pos.Y), new(pos.X-2, pos.Y), new(pos.X, pos.Y+2), new(pos.X, pos.Y-2)
              }
            : new Vector[] {
                new(pos.X+1, pos.Y), new(pos.X-1, pos.Y), new(pos.X, pos.Y+1), new(pos.X, pos.Y-1)
              };

        foreach (var n in steps)
        {
            if (n.X < 0 || n.Y < 0 || n.X >= map.Width || n.Y >= map.Height)
                continue;

            // Traversable cells only (same rule you already use)
            var st = map[n];
            if (st == CellState.Safe || st == CellState.Undiscovered || st == CellState.Reserved)
                yield return n;
        }
    }

    /// <summary>
    /// Edge cost: 1 for length-1 steps, 2 for jumps (length-2).
    /// </summary>
    public static int MoveCost(Vector from, Vector to)
    {
        int dx = Math.Abs(to.X - from.X);
        int dy = Math.Abs(to.Y - from.Y);
        int manhattan = dx + dy;          // will be 1 or 2 in this movement model
        return (manhattan == 2) ? 2 : 1;  // jump costs 2, step costs 1
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
    public Vector? PickClosestUndiscovered(Vector point, bool allowJumps)
    {
        if (this[point] == CellState.Undiscovered)
            return point;

        // Dijkstra
        var visited = new HashSet<Vector>();
        var gScore = new Dictionary<Vector, int> { [point] = 0 };
        var pq = new PriorityQueue<Vector, int>();
        pq.Enqueue(point, 0);

        while (pq.Count > 0)
        {
            var current = pq.Dequeue();
            if (!visited.Add(current))
                continue;

            if (this[current] == CellState.Undiscovered)
                return current; // first popped goal is truly closest by path cost

            foreach (var n in NeighborUtils.GetNeighbors(this, current, allowJumps))
            {
                int tentative = gScore[current] + NeighborUtils.MoveCost(current, n);
                if (!gScore.TryGetValue(n, out int old) || tentative < old)
                {
                    gScore[n] = tentative;
                    pq.Enqueue(n, tentative);
                }
            }
        }

        // BFS
        visited.Clear();
        var queue = new Queue<Vector>();
        queue.Enqueue(point);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            int x = current.X, y = current.Y;

            if (x < 0 || y < 0 || x >= Width || y >= Height || visited.Contains(current))
                continue;

            visited.Add(current);

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
