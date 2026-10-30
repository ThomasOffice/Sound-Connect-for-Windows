namespace Xm6Control.Cli;

internal sealed class ScanCliOptions
{
    public int Seconds { get; private set; } = 15;
    public string Output { get; private set; } = Path.Combine("docs", "ble-scan.json");
    public string FullOutput { get; private set; } = Path.Combine("artifacts", "ble-scan.full.json");
    public bool ShowHelp { get; private set; }

    public static ScanCliOptions Parse(string[] args)
    {
        var options = new ScanCliOptions();

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            switch (argument)
            {
                case "-h":
                case "--help":
                    options.ShowHelp = true;
                    return options;
                case "--seconds":
                    var value = NextValue(args, ref index, argument);
                    if (!int.TryParse(value, out var seconds) || seconds is < 1 or > 120)
                    {
                        throw new ArgumentException("--seconds must be an integer from 1 to 120.");
                    }

                    options.Seconds = seconds;
                    break;
                case "--output":
                    options.Output = NextValue(args, ref index, argument);
                    break;
                case "--full-output":
                    options.FullOutput = NextValue(args, ref index, argument);
                    break;
                default:
                    throw new ArgumentException($"Unknown scan argument: {argument}");
            }
        }

        return options;
    }

    private static string NextValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"{option} requires a value.");
        }

        return args[++index];
    }
}
