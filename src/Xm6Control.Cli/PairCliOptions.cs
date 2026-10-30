namespace Xm6Control.Cli;

internal sealed class PairCliOptions
{
    public string Name { get; private set; } = "LE_WH-1000XM6";
    public bool Repair { get; private set; }
    public bool Confirmed { get; private set; }

    public static PairCliOptions Parse(string[] args)
    {
        var options = new PairCliOptions();

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--name":
                    options.Name = NextValue(args, ref index, args[index]);
                    break;
                case "--repair":
                    options.Repair = true;
                    break;
                case "--yes":
                    options.Confirmed = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown pair argument: {args[index]}");
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
