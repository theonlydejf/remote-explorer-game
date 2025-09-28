using System.Reflection;

static class Config
{
    [CLIHelp("Initial map width.")]
    public static uint MAP_START_WIDTH = 10;
    [CLIHelp("Initial map height.")]
    public static uint MAP_START_HEIGHT = 10;
    [CLIHelp("How much to grow the map in one dimension.")]
    [CLIConfigCheckBounds(1, double.PositiveInfinity)]
    public static uint MAP_GROW_SIZE_1D = 6;
    [CLIHelp("How much to grow the map in two dimensions.")]
    [CLIConfigCheckBounds(1, double.PositiveInfinity)]
    public static uint MAP_GROW_SIZE_2D = 3;

    [CLIHelp("Should the agents use VSIDs?")]
    public static bool AGENT_USE_VSID = true;
    [CLIHelp("Prefix used in agent's VSID.")]
    public static char AGENT_VSID_PREFIX = '[';
    [CLIHelp("Color used in agent's VSID.")]
    public static ConsoleColor AGENT_VSID_COLOR = ConsoleColor.Blue;
    [CLIHelp("Total number of agents.")]
    public static uint AGENT_CNT = 5;
    [CLIHelp("Number of the total agents should have jumping enabled.")]
    public static uint AGENT_JUMPER_CNT = 5;
    [CLIHelp("Max tries for agent to find a destination.")]
    [CLIConfigCheckBounds(1, double.PositiveInfinity)]
    public static uint AGENT_MAX_TRIES = 10;

    [CLIHelp("Server IP address.")]
    public static string SERVER_IP = "127.0.0.1";
    [CLIHelp("Server port.")]
    public static uint SERVER_PORT = 8080;
    [CLIHelp("Player name.")]
    public static string PLAYER_NAME = "Example";

    [CLIHelp("Display update interval in milliseconds.")]
    [CLIConfigCheckBounds(1, double.PositiveInfinity)]
    public static uint DISPLAY_UPDATE_INTERVAL_MS = 20;

    /// <summary>
    /// Updates config fields from CLI args in the form --field=value. Case insensitive, udnerscores
    /// are replacable by dashes
    /// </summary>
    public static void ApplyArgs(string[] args)
    {
        bool error = false;
        foreach (var arg in args)
        {
            if (!arg.StartsWith("--"))
                continue;

            var split = arg.Substring(2).Split('=', 2);
            if (split.Length != 2)
                continue;

            var name = split[0].Replace("-", "_").ToUpperInvariant();
            var value = split[1];

            var field = typeof(Config).GetFields(BindingFlags.Static | BindingFlags.Public)
                .FirstOrDefault(f => f.Name.ToUpperInvariant() == name);
            
            if (field == null)
            {
                error = true;
                Console.Write("[");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("Warning");
                Console.ResetColor();
                Console.WriteLine($"] Unknown config identifier '{name}'. Ignoring...");
                continue;
            }

            try
            {
                object converted = field.FieldType.IsEnum
                    ? Enum.Parse(field.FieldType, value, true)
                    : Convert.ChangeType(value, field.FieldType);
                
                var attr = field.GetCustomAttribute<CLIConfigCheckBounds>();
                if (attr != null && !attr.IsInBounds(converted))
                {
                    error = true;
                    Console.Write("[");
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write("Warning");
                    Console.ResetColor();
                    Console.WriteLine($"] Value {value} is not in bounds for {field.Name} (min={attr.Min}, max={attr.Max}). Ignoring...");
                    continue;
                }

                field.SetValue(null, converted);
            }
            catch
            {
                error = true;
                Console.Write("[");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("Warning");
                Console.ResetColor();
                Console.WriteLine($"] Could not set config {field.Name} to {value}. Ignoring...");
            }
        }

        if (error)
        {
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
        }
    }

    public static void PrintHelp()
    {
        Console.WriteLine("Available configuration options (use --option=value):");

        foreach (var field in typeof(Config).GetFields(BindingFlags.Static | BindingFlags.Public))
        {
            var helpAttr = field.GetCustomAttribute<CLIHelpAttribute>();
            string help = helpAttr != null ? helpAttr.HelpText : "";
            var boundsAttr = field.GetCustomAttribute<CLIConfigCheckBounds>();
            string boundsInfo = boundsAttr == null ? "" : $" from <{boundsAttr.Min}, {boundsAttr.Max}>";

            string optionName = $"--{field.Name.ToLower().Replace("_", "-")}";
            string valueStr = $"(default: {field.GetValue(null)})";
            string typeStr = $"{field.FieldType.Name}{boundsInfo}";
            string helpStr = string.IsNullOrWhiteSpace(help) ? "" : help;

            Console.WriteLine($"  {optionName + " " + valueStr,-45} {typeStr,-25}{helpStr}");
        }
    }
}

[AttributeUsage(AttributeTargets.Field)]
public class CLIHelpAttribute : Attribute
{
    public string HelpText { get; }

    public CLIHelpAttribute(string helpText)
    {
        HelpText = helpText;
    }
}

[AttributeUsage(AttributeTargets.Field)]
public class CLIConfigCheckBounds : Attribute
{
    // Use double.MinValue and double.MaxValue as sentinels for "no bound"
    public double Min { get; }
    public double Max { get; }

    public CLIConfigCheckBounds(double min, double max)
    {
        Min = min;
        Max = max;
    }

    /// <summary>
    /// Checks if the given value is within the bounds specified by this attribute.
    /// Supports numeric types (int, uint, float, double, long, etc.).
    /// </summary>
    public bool IsInBounds(object value)
    {
        if (value == null)
            return false;

        try
        {
            double d = Convert.ToDouble(value);
            return (!double.IsRealNumber(Min) || d >= Min) && (!double.IsRealNumber(Max) || d <= Max);
        }
        catch
        {
            return false;
        }
    }
}
