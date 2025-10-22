using ExplorerGame.Base;
using ExplorerGame.Core;
using ExplorerGame.ConsoleVisualizer;
using ExplorerGame.Net;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Shared lock for orchestrating console writes from background tasks.
/// Keeps the logger, visualizer, and status rows from overlapping.
/// </summary>
public static class ConsoleSync
{
    public static object sync = new object();
}

enum LessonLogLevel
{
    Trace = 0,
    Info = 1,
    Warn = 2,
    Error = 3
}

/// <summary>
/// Minimal file logger used to mirror key events from the console to disk.
/// </summary>
class FileLogSink : IDisposable
{
    private readonly StreamWriter writer;
    private readonly object sync = new object();
    private bool disposed;

    public LessonLogLevel MinimumLevel { get; }

    public FileLogSink(string path, LessonLogLevel level)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        writer = new StreamWriter(path, append: true) { AutoFlush = true };
        MinimumLevel = level;
    }

    public void Write(LessonLogLevel level, string message)
    {
        if (level < MinimumLevel || disposed)
            return;

        lock (sync)
        {
            if (disposed)
                return;
            writer.WriteLine($"{DateTime.UtcNow:O} [{level}] {message}");
        }
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed)
                return;
            disposed = true;
            writer.Dispose();
        }
    }
}

class LessonExecConfig
{
    public string? ResourcesPath { get; private set; }
    public int Port { get; private set; } = 8080;
    public int ChallengePortStart { get; private set; } = 8081;
    public bool NoVisualizer { get; private set; }
    public int MaxSessionsPerClient { get; private set; } = 20;
    public TimeSpan IdleTimeout { get; private set; } = TimeSpan.FromSeconds(5);
    public string? LogFile { get; private set; }
    public LessonLogLevel LogLevel { get; private set; } = LessonLogLevel.Info;
    public string? MapConfigPath { get; private set; }

    public static LessonExecConfig Parse(string[] args)
    {
        var config = new LessonExecConfig();

        // Hand-rolled switch keeps dependencies light and behaviour explicit for students.
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (!arg.StartsWith("--"))
                throw new ArgumentException($"Unrecognized argument '{arg}'.");

            string flag = arg[2..];
            switch (flag)
            {
                case "no-visualizer":
                    config.NoVisualizer = true;
                    break;
                case "resources":
                    config.ResourcesPath = Path.GetFullPath(GetValue(args, ref i, flag));
                    break;
                case "port":
                    config.Port = ParseInt(GetValue(args, ref i, flag), flag);
                    break;
                case "challenge-port-start":
                    config.ChallengePortStart = ParseInt(GetValue(args, ref i, flag), flag);
                    break;
                case "max-sessions":
                    config.MaxSessionsPerClient = ParseInt(GetValue(args, ref i, flag), flag);
                    if (config.MaxSessionsPerClient <= 0)
                        throw new ArgumentException("--max-sessions must be greater than zero.");
                    break;
                case "idle-timeout":
                    config.IdleTimeout = ParseTimeSpanSeconds(GetValue(args, ref i, flag));
                    if (config.IdleTimeout <= TimeSpan.Zero)
                        throw new ArgumentException("--idle-timeout must be positive.");
                    break;
                case "log-file":
                    config.LogFile = Path.GetFullPath(GetValue(args, ref i, flag));
                    break;
                case "log-level":
                    config.LogLevel = ParseLogLevel(GetValue(args, ref i, flag));
                    break;
                case "map-config":
                    config.MapConfigPath = Path.GetFullPath(GetValue(args, ref i, flag));
                    break;
                default:
                    throw new ArgumentException($"Unknown option '--{flag}'.");
            }
        }

        return config;
    }

    private static string GetValue(string[] args, ref int index, string flag)
    {
        if (index + 1 >= args.Length)
            throw new ArgumentException($"Missing value for '--{flag}'.");
        return args[++index];
    }

    private static int ParseInt(string value, string flag)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
            throw new ArgumentException($"Value '{value}' for '--{flag}' is not a valid integer.");
        return result;
    }

    private static TimeSpan ParseTimeSpanSeconds(string value)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds))
            throw new ArgumentException($"Invalid number of seconds '{value}' for --idle-timeout.");
        return TimeSpan.FromSeconds(seconds);
    }

    private static LessonLogLevel ParseLogLevel(string value)
    {
        if (Enum.TryParse<LessonLogLevel>(value, true, out var level))
            return level;
        throw new ArgumentException($"Invalid log level '{value}'. Valid values: trace, info, warn, error.");
    }
}

class LessonWorldDefinition
{
    public string? Name { get; set; }
    public string? Color { get; set; }
    public int Port { get; set; }
    public string Map { get; set; } = string.Empty;
    public bool Visualize { get; set; }
}

class WorldInfo
{
    public string Name { get; set; }
    public ConsoleColor Color { get; set; }
    public int Port { get; set; }
    public Tile?[,] Map { get; set; }
    public bool Visualize { get; set; }
    public string? MapPath { get; set; }
    public ConnectionHandler? ConnectionHandler { get; set; }

    public WorldInfo(string name, ConsoleColor color, int port, Tile?[,] map, bool visualize, string? mapPath = null)
    {
        Name = name;
        Color = color;
        Port = port;
        Map = map;
        Visualize = visualize;
        MapPath = mapPath;
    }
}

public partial class Program
{
    private enum VisualizerHAlignement
    {
        Left,
        Centered,
        Right
    }

    public static void Main(string[] args)
    {
        LessonExecConfig config;
        try
        {
            config = LessonExecConfig.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return;
        }

        using FileLogSink? fileLog = config.LogFile != null ? new FileLogSink(config.LogFile, config.LogLevel) : null;

        try
        {
            Run(config, fileLog);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            fileLog?.Write(LessonLogLevel.Error, $"Fatal error: {ex}");
        }
    }

    /// <summary>
    /// Bootstraps maps, spins up servers, and manages the interactive console experience.
    /// </summary>
    private static void Run(LessonExecConfig config, FileLogSink? fileLog)
    {
        if (config.MapConfigPath != null && !File.Exists(config.MapConfigPath))
            throw new FileNotFoundException("Map configuration file not found.", config.MapConfigPath);

        string resourcesPath = config.ResourcesPath ?? Path.Combine(AppContext.BaseDirectory, "resources");
        resourcesPath = Path.GetFullPath(resourcesPath);

        fileLog?.Write(LessonLogLevel.Info, $"Using resources path: {resourcesPath}");
        if (config.MapConfigPath != null)
            fileLog?.Write(LessonLogLevel.Info, $"Using map configuration: {config.MapConfigPath}");

        List<WorldInfo> worlds;
        string[] candidateDirs = Array.Empty<string>();
        string[] challengeFiles = Array.Empty<string>();
        List<(string FileName, List<string> Paths)> duplicateChallengeFiles = new();

        if (config.MapConfigPath != null)
        {
            worlds = LoadWorldsFromConfig(config);
        }
        else
        {
            worlds = BuildDefaultWorlds(config, resourcesPath, out candidateDirs, out challengeFiles, out duplicateChallengeFiles);
        }

        if (worlds.Count == 0)
            throw new InvalidOperationException("No worlds configured.");

        var visualWorlds = worlds.Where(w => w.Visualize && !config.NoVisualizer).ToList();
        if (visualWorlds.Count > 1)
            throw new InvalidOperationException("Multiple visualized worlds configured; only one visualizer is supported.");
        WorldInfo? visualWorld = config.NoVisualizer ? null : visualWorlds.FirstOrDefault();
        Tile?[,] referenceMap = visualWorld?.Map ?? worlds[0].Map;

        Logger logger;
        bool winTooSmall;
        while (true)
        {
            winTooSmall = referenceMap != null && Console.WindowWidth < referenceMap.GetLength(0) * 2 + 2;

            Console.CursorVisible = false;
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            string exitMsg = "Press ESC or Q to exit";
            Console.SetCursorPosition(Math.Max(Console.WindowWidth / 2 - exitMsg.Length / 2, 0), Console.WindowHeight - 1);
            Console.Write(exitMsg);
            Console.ResetColor();

            try
            {
                int loggerTop = referenceMap != null ? referenceMap.GetLength(1) + 3 : 5;
                logger = new Logger(
                    left: 1,
                    top: loggerTop,
                    width: Math.Max(Console.WindowWidth - 3, 40),
                    height: Math.Max(Console.WindowHeight - loggerTop - 2, 10),
                    Console.ForegroundColor,
                    Console.BackgroundColor,
                    Console.ForegroundColor,
                    Console.BackgroundColor,
                    ConsoleSync.sync);
                if (!winTooSmall)
                    break;
            }
            catch
            {
                winTooSmall = true;
            }

            if (winTooSmall && referenceMap != null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                string message = "Terminal window is too small\nResize it and press any key to try again...";
                var lines = message.Split('\n');
                int top = Console.WindowHeight / 2 - lines.Length / 2;
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    int left = (Console.WindowWidth - line.Length) / 2;
                    Console.SetCursorPosition(left > 0 ? left : 0, top + i > 0 ? top + i : 0);
                    Console.Write(line);
                }
                Console.CursorVisible = true;
                if (IsExitKey(Console.ReadKey(true).Key))
                {
                    CleanUp();
                    return;
                }
            }
        }

        if (config.MapConfigPath == null)
        {
            // Default discovery is intentionally friendly: surface warnings rather than fail fast
            // so classrooms can spot path mistakes quickly.
            if (candidateDirs.Length == 0)
            {
                logger.WriteLine("No challenge directories found in CWD or BaseDir.", ConsoleColor.Black, ConsoleColor.Yellow);
                logger.WriteLine($"  CWD: {Directory.GetCurrentDirectory()}", ConsoleColor.DarkGray);
                logger.WriteLine($"  BaseDir: {AppContext.BaseDirectory}", ConsoleColor.DarkGray);
                fileLog?.Write(LessonLogLevel.Warn, "No challenge directories found in default locations.");
            }
            else if (challengeFiles.Length == 0)
            {
                logger.WriteLine("No challenge PNGs found (pattern 'challenge-*.png').", ConsoleColor.Black, ConsoleColor.Yellow);
                foreach (var d in candidateDirs)
                    logger.WriteLine($"  Searched: {d}", ConsoleColor.DarkGray);
                fileLog?.Write(LessonLogLevel.Warn, "Challenge directory located but no challenge-*.png files found.");
            }

            if (duplicateChallengeFiles.Count > 0)
            {
                logger.WriteLine("Duplicate challenge files found:", ConsoleColor.Black, ConsoleColor.Yellow);
                foreach (var duplicate in duplicateChallengeFiles)
                {
                    logger.WriteLine($"  {duplicate.FileName}", ConsoleColor.Yellow);
                    foreach (var path in duplicate.Paths)
                        logger.WriteLine($"    {path}", ConsoleColor.DarkGray);
                    var chosen = challengeFiles.FirstOrDefault(f =>
                        Path.GetFileName(f).Equals(duplicate.FileName, StringComparison.OrdinalIgnoreCase));
                    if (chosen != null)
                        logger.WriteLine($"    -> Using {chosen}", ConsoleColor.Green);

                    fileLog?.Write(LessonLogLevel.Warn,
                        $"Duplicate challenge file '{duplicate.FileName}' detected: {string.Join(", ", duplicate.Paths)}");
                }
            }
        }

        ConsoleVisualizer? viz = null;
        if (visualWorld != null)
        {
            int vizWidth = visualWorld.Map.GetLength(0) * 2 + 2;
            int vizLeft = Math.Max(Console.WindowWidth / 2 - vizWidth / 2, 0);
            viz = new ConsoleVisualizer(new Vector(vizLeft, 0), ConsoleSync.sync);
            viz.AttachMap(visualWorld.Map);
        }

        CancellationTokenSource cts = new();
        List<Task> serverTasks = new();

        // Local factory captures the configuration so each world reuses the same limits.
        ConnectionHandlerOptions BuildHandlerOptions(bool requireVSID) => new ConnectionHandlerOptions
        {
            MaxSessionsPerClient = config.MaxSessionsPerClient,
            IdleTimeout = config.IdleTimeout,
            SessionActionCooldown = TimeSpan.FromMilliseconds(50),
            RequireVSID = requireVSID
        };

        VisualizerHAlignement UpdateServerStatus()
        {
            const int MAX_EXPECTED_POSTFIX_LEN = 3;
            VisualizerHAlignement vizLoc = VisualizerHAlignement.Centered;
            int maxLen = 0;
            // Align logger rows with the widest world name so the status column stays tidy.
            foreach (var world in worlds)
            {
                int len = world.Name.Length + MAX_EXPECTED_POSTFIX_LEN;
                if (len > maxLen)
                    maxLen = len;
            }

            if (visualWorld != null)
            {
                int mapWidth = visualWorld.Map.GetLength(0);
                if (maxLen >= Console.WindowWidth - mapWidth * 2 - 3)
                    vizLoc = VisualizerHAlignement.Centered;
                else if (maxLen >= Console.WindowWidth / 2 - (mapWidth * 2 + 2) / 2 + 1)
                    vizLoc = VisualizerHAlignement.Right;
                else
                    vizLoc = VisualizerHAlignement.Centered;
            }

            // Refresh dashboard lines atomically with the console lock.
            lock (ConsoleSync.sync)
            {
                int mapHeight = visualWorld?.Map.GetLength(1) ?? referenceMap!.GetLength(1);
                int top = (mapHeight + 1) / 2 - worlds.Count / 2;
                if (top < 0)
                    top = 0;
                Console.SetCursorPosition(0, top);
                foreach (var world in worlds)
                {
                    Console.Write(' ');
                    Console.ForegroundColor = world.Color;
                    Console.Write(world.Name);
                    Console.ResetColor();
                    Console.Write(new string(' ', Math.Max(0, maxLen - MAX_EXPECTED_POSTFIX_LEN - world.Name.Length)));

                    string agents = world.ConnectionHandler != null
                        ? world.ConnectionHandler.SessionCount.ToString(CultureInfo.InvariantCulture)
                        : "ERR";
                    Console.WriteLine($" on {world.Port}: {agents,MAX_EXPECTED_POSTFIX_LEN} agents  ");
                }
            }
            return vizLoc;
        }

        VisualizerHAlignement initialAlignment = UpdateServerStatus();
        if (viz != null && visualWorld != null)
        {
            if (initialAlignment == VisualizerHAlignement.Left)
                viz.WindowLocation = new Vector(1, 0);
            else if (initialAlignment == VisualizerHAlignement.Right)
            {
                int newLeft = Math.Max(Console.WindowWidth - visualWorld.Map.GetLength(0) * 2 - 4, 0);
                viz.WindowLocation = new Vector(newLeft, 0);
            }
            UpdateServerStatus();
        }

        foreach (var world in worlds)
        {
            var handlerOptions = BuildHandlerOptions(world.Visualize && viz != null);
            var handler = new ConnectionHandler(world.Map, world.Visualize && viz != null ? viz : null, handlerOptions);
            world.ConnectionHandler = handler;

            handler.SessionConnected += new SessionConnectedLogger(
                logger,
                world,
                () => UpdateServerStatus(),
                fileLog
            ).Handler;

            handler.SessionConnected += (sender, e) => UpdateServerStatus();

            // Fire-and-forget HTTP listener; graceful shutdown is handled via cts.Cancel().
            serverTasks.Add(handler.StartHttpServer(world.Port, cts.Token));

            logger.Write(world.Name, world.Color);
            logger.WriteLine($" server started on port {world.Port}", ConsoleColor.Yellow);
            fileLog?.Write(LessonLogLevel.Info, $"{world.Name} server started on port {world.Port} (map: {world.MapPath ?? "embedded"})");
        }

        while (!IsExitKey(Console.ReadKey(true).Key))
        {
            // wait for exit
        }

        CleanUp();
        cts.Cancel();
        fileLog?.Write(LessonLogLevel.Info, "Shutdown requested by user.");
    }

    /// <summary>
    /// Discovers the bundled test world and challenge maps using the legacy naming scheme.
    /// </summary>
    private static List<WorldInfo> BuildDefaultWorlds(
        LessonExecConfig config,
        string resourcesPath,
        out string[] candidateDirs,
        out string[] chosenChallengeFiles,
        out List<(string FileName, List<string> Paths)> duplicates)
    {
        string cwd = Directory.GetCurrentDirectory();
        string baseDir = AppContext.BaseDirectory;

        var candidates = new List<string>
        {
            Path.Combine(cwd, "resources", "challenges"),
            Path.Combine(baseDir, "resources", "challenges"),
            Path.Combine(resourcesPath, "challenges"),
            resourcesPath
        };

        candidateDirs = candidates
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var re = new Regex(@"^challenge-(\d+)\.png$", RegexOptions.IgnoreCase);
        var allChallengeFiles = candidateDirs
            .SelectMany(dir => Directory.GetFiles(dir, "*.png")
                .Where(f => re.IsMatch(Path.GetFileName(f))))
            .ToList();

        duplicates = allChallengeFiles
            .GroupBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => (g.Key, g.ToList()))
            .ToList();

        chosenChallengeFiles = allChallengeFiles
            .GroupBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var cwdFile = g.FirstOrDefault(p => p.StartsWith(cwd, StringComparison.OrdinalIgnoreCase));
                return cwdFile ?? g.First();
            })
            .OrderBy(f => int.Parse(re.Match(Path.GetFileName(f)).Groups[1].Value, CultureInfo.InvariantCulture))
            .ToArray();

        var worlds = new List<WorldInfo>();

        string testMapPath = Path.Combine(resourcesPath, "test-map.png");
        var testWorldMap = GameFactory.MapFromImage(testMapPath);
        // Test world is always present and, unless suppressed, owns the visualizer.
        worlds.Add(new WorldInfo("Test World", ConsoleColor.Cyan, config.Port, testWorldMap, !config.NoVisualizer, testMapPath));

        for (int i = 0; i < chosenChallengeFiles.Length; i++)
        {
            string file = chosenChallengeFiles[i];
            Tile?[,] map = GameFactory.MapFromImage(file);
            worlds.Add(new WorldInfo(
                $"Challenge {i + 1}",
                ConsoleColor.Green,
                config.ChallengePortStart + i,
                map,
                visualize: false,
                mapPath: file));
        }

        return worlds;
    }

    private static List<WorldInfo> LoadWorldsFromConfig(LessonExecConfig config)
    {
        string json = File.ReadAllText(config.MapConfigPath!);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());

        var definitions = JsonSerializer.Deserialize<List<LessonWorldDefinition>>(json, options)
            ?? throw new InvalidOperationException("Map configuration file is empty or invalid.");

        // Map paths resolve relative to the config file so classrooms can ship self-contained bundles.
        string baseDir = Path.GetDirectoryName(config.MapConfigPath!) ?? Directory.GetCurrentDirectory();
        var worlds = new List<WorldInfo>();

        for (int i = 0; i < definitions.Count; i++)
        {
            LessonWorldDefinition def = definitions[i];
            if (string.IsNullOrWhiteSpace(def.Map))
                throw new ArgumentException($"World definition at index {i} is missing a map path.");

            string mapPath = def.Map;
            if (!Path.IsPathRooted(mapPath))
                mapPath = Path.GetFullPath(Path.Combine(baseDir, mapPath));

            if (!File.Exists(mapPath))
                throw new FileNotFoundException($"Map file '{mapPath}' not found.", mapPath);

            Tile?[,] map = GameFactory.MapFromImage(mapPath);

            ConsoleColor color = ConsoleColor.White;
            if (!string.IsNullOrWhiteSpace(def.Color))
                color = Enum.Parse<ConsoleColor>(def.Color, true);

            string name = string.IsNullOrWhiteSpace(def.Name) ? $"World {i + 1}" : def.Name;
            int port = def.Port != 0 ? def.Port : config.Port + i;

            bool visualize = def.Visualize && !config.NoVisualizer;

            worlds.Add(new WorldInfo(name, color, port, map, visualize, mapPath));
        }

        if (worlds.All(w => !w.Visualize) && !config.NoVisualizer && worlds.Count > 0)
        {
            // default first world to visualized if none specified
            worlds[0].Visualize = true;
        }

        return worlds;
    }

    static bool IsExitKey(ConsoleKey key) => new[] { ConsoleKey.Escape, ConsoleKey.Q }.Contains(key);

    static void CleanUp()
    {
        Console.CursorVisible = true;
        Console.Clear();
    }
}

class SessionConnectedLogger
{
    private Logger logger;
    private string world;
    private ConsoleColor worldColor;
    private Action? updateAgentStats;
    private FileLogSink? fileLog;

    public SessionConnectedLogger(Logger logger, WorldInfo worldInfo, Action? updateAgentStats = null, FileLogSink? fileLog = null)
    {
        this.logger = logger;
        world = worldInfo.Name;
        worldColor = worldInfo.Color;
        this.updateAgentStats = updateAgentStats;
        this.fileLog = fileLog;
    }

    public void Handler(object? sender, SessionConnectedEventArgs e)
    {
        bool success = e.Response.Value<bool>("success");
        bool attachAgentLogger = false;
        LessonLogLevel? fileLevel = null;
        string? fileMessage = null;

        lock (ConsoleSync.sync)
        {
            logger.Write("[");
            logger.Write(e.ClientUsername, ConsoleColor.Yellow);
            logger.Write(" @" + e.ClientID, ConsoleColor.DarkGray);
            logger.Write("] ");
            if (!success)
            {
                string message = e.Response.Value<string>("message") ?? "Unknown error";
                logger.Write("Agent connection failed (", ConsoleColor.Red);
                logger.Write(world, worldColor);
                logger.WriteLine($") -> {message}", ConsoleColor.Red);
                fileLevel = LessonLogLevel.Warn;
                fileMessage = $"[{e.ClientUsername}@{e.ClientID}] Connection to {world} failed: {message}";
                return;
            }

            logger.Write("Connected ");
            if (e.SessionIdentifier != null && e.SessionIdentifier.HasVSID)
            {
                logger.Write("with '");
                logger.Write(e.SessionIdentifier.IdentifierStr, e.SessionIdentifier.Color.Value);
                logger.Write("' ");
            }
            logger.Write("into ");
            logger.WriteLine(world, worldColor);

            if (e.GameSession == null)
            {
                logger.WriteLine("Connection without session detected!", ConsoleColor.White, ConsoleColor.Red);
                fileLevel = LessonLogLevel.Error;
                fileMessage = $"[{e.ClientUsername}@{e.ClientID}] Connection to {world} returned no session instance.";
                return;
            }

            attachAgentLogger = true;
            fileLevel = LessonLogLevel.Info;
            string vsid = e.SessionIdentifier != null && e.SessionIdentifier.HasVSID
                ? e.SessionIdentifier.IdentifierStr ?? "(unknown)"
                : "(no VSID)";
            fileMessage = $"[{e.ClientUsername}@{e.ClientID}] Connected to {world} with VSID {vsid}";
        }

        if (attachAgentLogger && e.GameSession != null)
        {
            e.GameSession.AgentDied += new AgentDiedLogger(e, logger, world, worldColor, fileLog).Handler;
            e.GameSession.AgentDied += (sender, args) => updateAgentStats?.Invoke();
        }

        if (fileLevel.HasValue && fileMessage != null)
            fileLog?.Write(fileLevel.Value, fileMessage);
    }
}

class AgentDiedLogger
{
    private SessionConnectedEventArgs sessionArgs;
    private Logger logger;
    private string world;
    private ConsoleColor worldColor;
    private FileLogSink? fileLog;

    public AgentDiedLogger(SessionConnectedEventArgs sessionArgs, Logger logger, string world, ConsoleColor worldColor, FileLogSink? fileLog)
    {
        this.sessionArgs = sessionArgs;
        this.logger = logger;
        this.world = world;
        this.worldColor = worldColor;
        this.fileLog = fileLog;
    }

    public void Handler(object? sender, AgentDiedEventArgs e)
    {
        lock (ConsoleSync.sync)
        {
            logger.Write("[");
            logger.Write(sessionArgs.ClientUsername, ConsoleColor.Yellow);
            logger.Write(" @" + sessionArgs.ClientID, ConsoleColor.DarkGray);
            logger.Write("] Died in '");
            logger.Write(world, worldColor);
            logger.WriteLine($"' (reason: '{e.DeathReason}')");
        }

        fileLog?.Write(LessonLogLevel.Warn,
            $"[{sessionArgs.ClientUsername}@{sessionArgs.ClientID}] Agent died in '{world}' (reason: {e.DeathReason})");
    }
}
