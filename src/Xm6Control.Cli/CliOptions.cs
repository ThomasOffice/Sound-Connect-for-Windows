using Xm6Control.Bluetooth.Windows;
using Xm6Control.Protocol;

namespace Xm6Control.Cli;

internal sealed class CliOptions
{
    public const string HelpText = """
        XM6 Control CLI

        Commands:
          scan                   Scan BLE advertisements
          probe                  Enumerate GATT and read device information
          tandem                 Attempt the Tandem v2 handshake
          features               Query NCASM/EQ/power/GS read-only state
          eq                     Write EQ preset/bands and read back
          anc                    Write NC/ASM mode and read back
          multipoint             Enable or disable multipoint and read back
          autopower              Set auto power off and read back
          listening              Set standard/BGM/cinema and read back
          pair                   Pair or repair the BLE device

        Usage:
          Xm6Control.Cli probe [options]

        Options:
          --name <name>          Target name (default: WH-1000XM6)
          --address <address>    Target Bluetooth address, e.g. AA:BB:CC:DD:EE:FF
          --transport <mode>     auto, ble, or rfcomm (default: auto)
          --address-type <type>  auto, public, or random (default: auto)
          --bootstrap            Try the known pairing-name write before Tandem
          --preset <name|0xNN>   EQ preset for the eq command
          --bands <1,2,...>      EQ band values for the eq command
          --mode <nc|asm|off>    NC/ASM mode for the anc command
          --level <1..20>        Ambient level for the anc command
          --noise-adaptive <on|off>  Automatic ambient sound
          --sensitivity <value>  standard, high, or low
          --voice-focus <on|off> Focus on voice in ambient mode
          --multipoint <on|off>  Multipoint state for the multipoint command
          --autopower <value>    off or removed
          --listening <mode>     standard, bgm, or cinema
          --room <size>          small/myroom, middle/living, or large/cafe
          --yes                  Confirm the state-changing write
          --wait <milliseconds>  Notification observation window (default: 5000)
          --output <path>        Redacted report path (default: docs/probe-xm6.json)
          --full-output <path>   Full local report path (default: artifacts/probe-xm6.full.json)
          -h, --help             Show this help

        Exit codes:
          0  GATT facts read, or RFCOMM services found
          1  Error
          2  GATT enumerated but no standard device fact read
          3  Cancelled
        """;

    public string Name { get; private set; } = "WH-1000XM6";
    public string? Address { get; private set; }
    public string Transport { get; private set; } = "auto";
    public string AddressType { get; private set; } = "auto";
    public bool BootstrapTandem { get; private set; }
    public int WaitMs { get; private set; } = 5_000;
    public string Output { get; private set; } = Path.Combine("docs", "probe-xm6.json");
    public string FullOutput { get; private set; } = Path.Combine("artifacts", "probe-xm6.full.json");
    public bool ApplyEq { get; private set; }
    public TandemEqPresetId? EqPreset { get; private set; }
    public IReadOnlyList<byte>? EqBands { get; private set; }
    public bool ApplyNcAsm { get; private set; }
    public TandemNcAsmMode? AncMode { get; private set; }
    public byte AncLevel { get; private set; } = 20;
    public bool AncEnabled { get; private set; } = true;
    public bool AncVoiceFocus { get; private set; }
    public bool AncNoiseAdaptive { get; private set; }
    public TandemNoiseAdaptiveSensitivity AncSensitivity { get; private set; } =
        TandemNoiseAdaptiveSensitivity.Standard;
    public bool ApplyMultipoint { get; private set; }
    public bool? MultipointEnabled { get; private set; }
    public bool ApplyAutoPowerOff { get; private set; }
    public TandemAutoPowerOffElement? AutoPowerOffElement { get; private set; }
    public bool ApplyListeningMode { get; private set; }
    public TandemListeningMode? ListeningMode { get; private set; }
    public TandemRoomSize? BgmRoomSize { get; private set; }
    public bool Confirmed { get; private set; }
    public bool ShowHelp { get; private set; }

    public ProbeRequest ToRequest(
        bool queryFeatures = false,
        bool applyEq = false,
        bool applyNcAsm = false,
        bool applyMultipoint = false,
        bool applyAutoPowerOff = false,
        bool applyListeningMode = false) => new()
        {
            Name = Name,
            Address = Address,
            Transport = Transport,
            AddressType = AddressType,
            BootstrapTandem = BootstrapTandem,
            NotificationWaitMs = WaitMs,
            QueryFeatures = queryFeatures,
            ApplyEq = applyEq,
            EqPreset = EqPreset,
            EqBands = EqBands,
            ApplyNcAsm = applyNcAsm,
            NcAsmMode = AncMode,
            NcAsmLevel = AncLevel,
            NcAsmEnabled = AncEnabled,
            NcAsmVoiceFocus = AncVoiceFocus,
            NcAsmNoiseAdaptive = AncNoiseAdaptive,
            NcAsmSensitivity = AncSensitivity,
            ApplyMultipoint = applyMultipoint,
            MultipointEnabled = MultipointEnabled,
            ApplyAutoPowerOff = applyAutoPowerOff,
            AutoPowerOffElement = AutoPowerOffElement,
            ApplyListeningMode = applyListeningMode,
            ListeningMode = ListeningMode,
            BgmRoomSize = BgmRoomSize,
        };

    public static CliOptions Parse(string[] args)
    {
        var options = new CliOptions();
        var index = 0;

        if (args.Length > 0 && args[0].Equals("probe", StringComparison.OrdinalIgnoreCase))
        {
            index = 1;
        }
        else if (args.Length > 0 && args[0] is "-h" or "--help")
        {
            options.ShowHelp = true;
            return options;
        }

        for (; index < args.Length; index++)
        {
            var argument = args[index];
            switch (argument)
            {
                case "-h":
                case "--help":
                    options.ShowHelp = true;
                    return options;
                case "--name":
                    options.Name = NextValue(args, ref index, argument);
                    break;
                case "--address":
                    options.Address = NextValue(args, ref index, argument);
                    break;
                case "--transport":
                    options.Transport = NextValue(args, ref index, argument).ToLowerInvariant();
                    if (options.Transport is not ("auto" or "ble" or "rfcomm"))
                    {
                        throw new ArgumentException("--transport must be auto, ble, or rfcomm.");
                    }

                    break;
                case "--address-type":
                    options.AddressType = NextValue(args, ref index, argument).ToLowerInvariant();
                    if (options.AddressType is not ("auto" or "public" or "random"))
                    {
                        throw new ArgumentException("--address-type must be auto, public, or random.");
                    }

                    break;
                case "--bootstrap":
                    options.BootstrapTandem = true;
                    break;
                case "--yes":
                    options.Confirmed = true;
                    break;
                case "--preset":
                    options.EqPreset = ParsePreset(NextValue(args, ref index, argument));
                    break;
                case "--bands":
                    options.EqBands = ParseBands(NextValue(args, ref index, argument));
                    break;
                case "--mode":
                    var modeValue = NextValue(args, ref index, argument);
                    if (modeValue.Equals("off", StringComparison.OrdinalIgnoreCase))
                    {
                        options.AncEnabled = false;
                        options.AncMode = null;
                    }
                    else
                    {
                        options.AncEnabled = true;
                        options.AncMode = ParseAncMode(modeValue);
                    }

                    break;
                case "--level":
                    if (!byte.TryParse(NextValue(args, ref index, argument), out var level)
                        || level is < 1 or > 20)
                    {
                        throw new ArgumentException("--level must be an integer from 1 to 20.");
                    }

                    options.AncLevel = level;
                    break;
                case "--noise-adaptive":
                    options.AncNoiseAdaptive = ParseOnOff(
                        NextValue(args, ref index, argument),
                        "--noise-adaptive");
                    break;
                case "--voice-focus":
                    options.AncVoiceFocus = ParseOnOff(
                        NextValue(args, ref index, argument),
                        "--voice-focus");
                    break;
                case "--sensitivity":
                    options.AncSensitivity = ParseSensitivity(
                        NextValue(args, ref index, argument));
                    break;
                case "--multipoint":
                    options.MultipointEnabled = ParseMultipoint(NextValue(args, ref index, argument));
                    break;
                case "--autopower":
                    options.AutoPowerOffElement = ParseAutoPowerOff(NextValue(args, ref index, argument));
                    break;
                case "--listening":
                    options.ListeningMode = ParseListeningMode(NextValue(args, ref index, argument));
                    break;
                case "--room":
                    options.BgmRoomSize = ParseRoomSize(NextValue(args, ref index, argument));
                    break;
                case "--wait":
                    var wait = NextValue(args, ref index, argument);
                    if (!int.TryParse(wait, out var waitMs) || waitMs < 0 || waitMs > 60_000)
                    {
                        throw new ArgumentException("--wait must be an integer from 0 to 60000.");
                    }

                    options.WaitMs = waitMs;
                    break;
                case "--output":
                    options.Output = NextValue(args, ref index, argument);
                    break;
                case "--full-output":
                    options.FullOutput = NextValue(args, ref index, argument);
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {argument}");
            }
        }

        return options;
    }

    private static TandemNcAsmMode ParseAncMode(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "nc" or "anc" or "noise" => TandemNcAsmMode.Nc,
            "asm" or "ambient" => TandemNcAsmMode.Asm,
            _ => throw new ArgumentException("--mode must be nc or asm."),
        };
    }

    private static bool ParseOnOff(string value, string option)
    {
        return value.ToLowerInvariant() switch
        {
            "on" or "enable" or "true" or "1" => true,
            "off" or "disable" or "false" or "0" => false,
            _ => throw new ArgumentException($"{option} must be on or off."),
        };
    }

    private static TandemNoiseAdaptiveSensitivity ParseSensitivity(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "standard" or "std" => TandemNoiseAdaptiveSensitivity.Standard,
            "high" => TandemNoiseAdaptiveSensitivity.High,
            "low" => TandemNoiseAdaptiveSensitivity.Low,
            _ => throw new ArgumentException("--sensitivity must be standard, high, or low."),
        };
    }

    private static bool ParseMultipoint(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "on" or "enable" or "true" or "1" => true,
            "off" or "disable" or "false" or "0" => false,
            _ => throw new ArgumentException("--multipoint must be on or off."),
        };
    }

    private static TandemListeningMode ParseListeningMode(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "standard" or "normal" or "std" => TandemListeningMode.Standard,
            "bgm" or "background" or "music" => TandemListeningMode.BackgroundMusic,
            "cinema" or "movie" => TandemListeningMode.Cinema,
            _ => throw new ArgumentException("--listening must be standard, bgm, or cinema."),
        };
    }

    private static TandemRoomSize ParseRoomSize(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "small" or "myroom" or "room" => TandemRoomSize.Small,
            "middle" or "living" or "livingroom" => TandemRoomSize.Middle,
            "large" or "cafe" => TandemRoomSize.Large,
            _ => throw new ArgumentException(
                "--room must be small/myroom, middle/living, or large/cafe."),
        };
    }

    private static TandemAutoPowerOffElement ParseAutoPowerOff(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "off" or "disable" or "none" => TandemAutoPowerOffElement.Disable,
            "removed" or "ears" => TandemAutoPowerOffElement.WhenRemovedFromEars,
            _ => throw new ArgumentException(
                "--autopower must be off or removed."),
        };
    }
    private static TandemEqPresetId ParsePreset(string value)
    {
        if (Enum.TryParse<TandemEqPresetId>(value, ignoreCase: true, out var preset)
            && Enum.IsDefined(preset))
        {
            return preset;
        }

        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            && byte.TryParse(value[2..], System.Globalization.NumberStyles.HexNumber, null, out var hex))
        {
            return (TandemEqPresetId)hex;
        }

        if (byte.TryParse(value, out var number))
        {
            return (TandemEqPresetId)number;
        }

        throw new ArgumentException($"Invalid EQ preset: {value}.");
    }

    private static IReadOnlyList<byte> ParseBands(string value)
    {
        var parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var bands = new byte[parts.Length];
        for (var index = 0; index < parts.Length; index++)
        {
            if (!byte.TryParse(parts[index], out bands[index]))
            {
                throw new ArgumentException($"Invalid EQ band value: {parts[index]}.");
            }
        }

        return bands;
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











