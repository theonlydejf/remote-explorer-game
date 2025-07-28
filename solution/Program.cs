using ExplorerGame.Core;
using ExplorerGame.Net;

RemoteGameSessionFactory factory = new RemoteGameSessionFactory("http://127.0.0.1:8087/", "David");

Map map = new Map(15, 15);
Agent[] agents = new Agent[5];
for (int i = 0; i < agents.Length; i++)
{
    agents[i] = new Agent(factory, getID(i), map, true);
}

DateTime lastDisplayUpdate = DateTime.UnixEpoch;
while (true)
{
    foreach (Agent agent in agents)
    {
        if (agent.result.Ready)
            agent.AfterStep();
    }

    if (DateTime.Now.Subtract(lastDisplayUpdate) < TimeSpan.FromMilliseconds(20))
        continue;
    Console.Clear();
    for (int y = 0; y < map.Height; y++)
    {
        for (int x = 0; x < map.Width; x++)
        {
            if (agents.Any(agent => !agent.nowhereToGo && agent.Pos == new Vector(x, y)))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("<>");
                continue;
            }

            switch (map[x, y])
            {
                case CellState.Safe:
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write("..");
                    break;
                case CellState.Trap:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("XX");
                    break;
                case CellState.Unreachable:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write("??");
                    break;
                case CellState.Reserved:
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.Write("**");
                    break;
                default:
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write("**");
                    break;
            }
        }
        Console.WriteLine();
    }
    if (agents.All(agent => agent.nowhereToGo))
        break;
    lastDisplayUpdate = DateTime.Now;
}
// Console.Clear();
// for (int y = 0; y < map.Height; y++)
// {
//     for (int x = 0; x < map.Width; x++)
//     {
//         switch (map[x, y])
//         {
//             case CellState.Safe:
//                 Console.ForegroundColor = ConsoleColor.White;
//                 Console.Write("..");
//                 break;
//             case CellState.Trap:
//                 Console.ForegroundColor = ConsoleColor.Red;
//                 Console.Write("XX");
//                 break;
//             case CellState.Unreachable:
//                 Console.ForegroundColor = ConsoleColor.Yellow;
//                 Console.Write("??");
//                 break;
//             default:
//                 Console.ForegroundColor = ConsoleColor.DarkGray;
//                 Console.Write("**");
//                 break;
//         }
//     }
//     Console.WriteLine();
// }


SessionIdentifier getID(int idx) => new SessionIdentifier("D" + idx, ConsoleColor.Blue);

enum CellState
{
    Undiscovered,
    Safe,
    Trap,
    Unreachable,
    Reserved
}

class Agent
{
    public static object Sync = new object();

    public Vector Destination { get; set; }

    public Vector Pos { get; set; }
    public RemoteGameSession GameSession { get; private set; }
    public SessionIdentifier Identifier { get; }
    public Map Map { get; }
    private RemoteGameSessionFactory factory;
    public AsyncMovementResult result;
    private Vector? currStep;
    private Vector? reserved;
    public bool allowJumps { get; set; }
    public bool nowhereToGo { get; private set; } = false;

    public Agent(RemoteGameSessionFactory factory, SessionIdentifier identifier, Map map, bool allowJumps)
    {
        this.factory = factory;
        GameSession = factory.Create(identifier);
        Identifier = identifier;
        Map = map;
        this.allowJumps = allowJumps;
        lock (Sync)
        {
            newDestination();
        }
        result = new AsyncMovementResult(); // Watrning ://
        Step();
    }

    private void Step()
    {
        if (!new[] { CellState.Undiscovered, CellState.Reserved }.Contains(Map[Destination])
            || currStep == new Vector(0, 0))
            newDestination();

        currStep = NextStep();
        if (currStep == new Vector(0, 0))
        {
            lock (Sync)
            {
                newDestination();
            }
            currStep = NextStep();
        }

        if (!allowJumps)
        {
            //  Limit tries v------------------------------v    v--------------v check if valid dest was found
            for (int i = 0; i < (Map.Width + Map.Height / 2) && currStep == null; i++)
            {
                newDestination();
                currStep = NextStep();
            }

            if (currStep == null)
            {
                currStep = new Vector(0, 0);
                releaseReservedLoc();
            }
        }
        else while (currStep == null)
        {
            lock (Sync)
            {
                Map[Destination] = CellState.Unreachable;
                newDestination();
            }
            currStep = NextStep();
        }

        result = GameSession.MoveAsync(currStep.Value);
    }

    private void newDestination()
    {
        Destination = closestPos();
        reserveLoc(Destination);
    }

    private void releaseReservedLoc()
    {
        if (!reserved.HasValue)
            return;

        if (Map[reserved.Value] == CellState.Reserved)
            Map[reserved.Value] = CellState.Undiscovered;
        return;
    }

    private void reserveLoc(Vector loc)
    {
        if (reserved.HasValue)
            releaseReservedLoc();
        
        reserved = loc;
        Map[reserved.Value] = CellState.Reserved;
    }

    public void AfterStep()
    {
        if (nowhereToGo)
            return;
        if (currStep == null)
        {
            Step();
            return;
        }
        Pos += currStep.Value;
        lock (Sync)
        {
            if (result.MovementResult?.IsAgentAlive == true)
            {
                Map[Pos] = CellState.Safe;
                goto prasarna;
            }

            if (result.MovementResult?.MovedSuccessfully == false)
                goto prasarna;

            GameSession = factory.Create(Identifier);
            Map[Pos] = CellState.Trap;
            Pos = new(0, 0);
            newDestination();
        }
    prasarna:
        Step();
    }

    Vector closestPos()
    {
        Vector? vec = Map.PickClosestUndiscovered(Pos);
        while (vec == null)
        {
            if (GrowMap(6, 3))
            {
                nowhereToGo = true;
                return new Vector(0, 0);
            }
            vec = Map.PickClosestUndiscovered(Pos);
        }
        return vec.Value;
    }
    Vector rndPos()
    {
        Vector? vec = Map.PickRandomUndiscovered(Random.Shared);
        while (vec == null)
        {
            if (GrowMap(6, 3))
            {
                nowhereToGo = true;
                return new Vector(0, 0);
            }
            vec = Map.PickRandomUndiscovered(Random.Shared);
        }
        return vec.Value;
    }

    // True when nowhere to go
    private bool GrowMap(int growSize1D = 2, int growSize2D = 1)
    {
        // Count unreachable cells on the right and bottom edges
        int rightScore = 0, bottomScore = 0;
        int unreachableRight = 0, unreachableBottom = 0;
        CellState[] badCells = { CellState.Unreachable, CellState.Trap };

        // Right edge (last column)
        for (int dx = 0; dx < 2; dx++)
        {
            for (int y = 0; y < Map.Height; y++)
            {
                if (badCells.Contains(Map[Map.Width - 1 - dx, y]))
                    rightScore++;
                if (Map[Map.Width - 1 - dx, y] == CellState.Unreachable)
                    unreachableRight++;
            }
        }
        for (int dy = 0; dy < 2; dy++)
        {
            // Bottom edge (last row)
            for (int x = 0; x < Map.Width; x++)
            {
                if (badCells.Contains(Map[x, Map.Height - 1 - dy]))
                    bottomScore++;
                if (Map[x , Map.Width - 1 - dy] == CellState.Unreachable)
                    unreachableBottom++;
            }
        }

        if (rightScore >= 2 * Map.Height)
        {
            if (bottomScore >= 2 * Map.Width)
                return true;

            Map.Grow(0, growSize1D);
            return false;
        }
        else if (bottomScore >= 2 * Map.Width)
        {
            Map.Grow(growSize1D, 0);
            return false;
        }

        // Grow more in the direction with more unreachable cells
        if (bottomScore > rightScore)
            Map.Grow(growSize1D, 0);
        else if (rightScore > bottomScore)
            Map.Grow(0, growSize1D);
        else
            Map.Grow(growSize2D, growSize2D);
        return false;
    }

    Vector? NextStep()
    {
        // A* algorithm
        var openSet = new SortedSet<(float fScore, int tieBreaker, Vector pos)>(Comparer<(float, int, Vector)>.Create((a, b) =>
        {
            int cmp = a.Item1.CompareTo(b.Item1);
            if (cmp != 0) return cmp;
            cmp = a.Item2.CompareTo(b.Item2);
            if (cmp != 0) return cmp;
            // Compare by X then Y for deterministic ordering
            cmp = a.Item3.X.CompareTo(b.Item3.X);
            if (cmp != 0) return cmp;
            return a.Item3.Y.CompareTo(b.Item3.Y);
        }));

        var cameFrom = new Dictionary<Vector, Vector>();
        var gScore = new Dictionary<Vector, float> { [Pos] = 0 };
        var fScore = new Dictionary<Vector, float> { [Pos] = Heuristic(Pos, Destination) };

        int tieBreaker = 0;
        openSet.Add((fScore[Pos], tieBreaker++, Pos));

        while (openSet.Count > 0)
        {
            var current = openSet.Min.pos;
            if (current.Equals(Destination))
                return ReconstructFirstStep(cameFrom, current, Pos) - Pos;

            openSet.Remove(openSet.Min);

            foreach (var neighbor in GetNeighbors(current))
            {
                float tentativeG = gScore[current] + 1;
                if (gScore.TryGetValue(neighbor, out float g) && tentativeG >= g)
                    continue;

                cameFrom[neighbor] = current;
                gScore[neighbor] = tentativeG;
                fScore[neighbor] = tentativeG + Heuristic(neighbor, Destination);
                openSet.Add((fScore[neighbor], tieBreaker++, neighbor));
            }
        }
        // No path found, stay in place
        return null;
    }

    float Heuristic(Vector a, Vector b)
    {
        // Manhattan distance
        return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    }

    IEnumerable<Vector> GetNeighbors(Vector pos)
    {
        int[] dx, dy;
        if (allowJumps)
        {
            dx = new int[] { 1, -1, 0, 0, 2, -2, 0, 0 };
            dy = new int[] { 0, 0, 1, -1, 0, 0, 2, -2 };
        }
        else
        {
            dx = new int[] { 1, -1, 0, 0 };
            dy = new int[] { 0, 0, 1, -1 };
        }
        for (int i = 0; i < dx.Length; i++)
        {
            int nx = pos.X + dx[i];
            int ny = pos.Y + dy[i];

            // bounds check here — critical!
            if (nx < 0 || ny < 0 || nx >= Map.Width || ny >= Map.Height)
                continue;

            // Only allow Safe or Undiscovered cells
            if (new CellState[] { CellState.Safe, CellState.Undiscovered, CellState.Reserved }.Contains(Map[nx, ny]))
                yield return new Vector(nx, ny);
        }
    }

    Vector ReconstructFirstStep(Dictionary<Vector, Vector> cameFrom, Vector current, Vector start)
    {
        Vector prev = current;
        while (cameFrom.TryGetValue(prev, out Vector parent) && !parent.Equals(start))
            prev = parent;
        return prev;
    }
}

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
            if (row < 0 || col < 0 || row >= Width || col >= Height)
                return CellState.Trap;
            return map[row, col];
        }
        set
        {
            if (row < 0 || col < 0 || row >= Width || col >= Height)
                return; // ignore out-of-bounds write
            if (map[row, col] == CellState.Undiscovered && value != CellState.Undiscovered)
                UndiscoveredCnt--;
            if (map[row, col] != CellState.Undiscovered && value == CellState.Undiscovered)
                UndiscoveredCnt++;
            map[row, col] = value;
        }
    }

    // Grows the map by increasing its width and height by the specified amounts
    public void Grow(int widthIncrease = 1, int heightIncrease = 1)
    {
        int newWidth = Width + widthIncrease;
        int newHeight = Height + heightIncrease;
        var newMap = new CellState[newWidth, newHeight];

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (map[x, y] == CellState.Unreachable)
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

    // Picks the undiscovered position closest to the given point using BFS, or returns null if none found
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

    // Picks a random undiscovered position, or returns null if none found
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