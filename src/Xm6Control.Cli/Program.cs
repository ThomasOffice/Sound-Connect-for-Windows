using Xm6Control.Bluetooth.Windows;
using Xm6Control.Protocol;

namespace Xm6Control.Cli;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length > 0 && args[0].Equals("scan", StringComparison.OrdinalIgnoreCase))
        {
            return await RunScanAsync(args.Skip(1).ToArray());
        }

        if (args.Length > 0 && args[0].Equals("pair", StringComparison.OrdinalIgnoreCase))
        {
            return await RunPairAsync(args.Skip(1).ToArray());
        }

        if (args.Length > 0 && args[0].Equals("tandem", StringComparison.OrdinalIgnoreCase))
        {
            return await RunTandemAsync(args.Skip(1).ToArray());
        }

        if (args.Length > 0 && args[0].Equals("features", StringComparison.OrdinalIgnoreCase))
        {
            return await RunTandemAsync(args.Skip(1).ToArray(), queryFeatures: true);
        }

        if (args.Length > 0 && args[0].Equals("eq", StringComparison.OrdinalIgnoreCase))
        {
            return await RunEqAsync(args.Skip(1).ToArray());
        }

        if (args.Length > 0 && args[0].Equals("anc", StringComparison.OrdinalIgnoreCase))
        {
            return await RunAncAsync(args.Skip(1).ToArray());
        }

        if (args.Length > 0 && args[0].Equals("multipoint", StringComparison.OrdinalIgnoreCase))
        {
            return await RunMultipointAsync(args.Skip(1).ToArray());
        }

        if (args.Length > 0 && args[0].Equals("autopower", StringComparison.OrdinalIgnoreCase))
        {
            return await RunAutoPowerAsync(args.Skip(1).ToArray());
        }

        if (args.Length > 0 && args[0].Equals("listening", StringComparison.OrdinalIgnoreCase))
        {
            return await RunListeningAsync(args.Skip(1).ToArray());
        }

        if (args.Length > 0 && args[0].Equals("classic", StringComparison.OrdinalIgnoreCase))
        {
            return await RunClassicAsync(args.Skip(1).ToArray());
        }

        CliOptions options;
        try
        {
            options = CliOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine();
            Console.Error.WriteLine(CliOptions.HelpText);
            return 1;
        }

        if (options.ShowHelp)
        {
            Console.WriteLine(CliOptions.HelpText);
            return 0;
        }

        Console.WriteLine($"Probing '{options.Name}' via {options.Transport}...");
        var report = await new BluetoothProbeService().ProbeAsync(options.ToRequest());

        WriteReport(options.FullOutput, JsonOutput.Serialize(report));
        WriteReport(options.Output, JsonOutput.SerializeRedacted(report));
        PrintSummary(report, options);

        return report.Status switch
        {
            "ok" or "rfcomm_found" => 0,
            "gatt_enumerated" => 2,
            "cancelled" => 3,
            _ => 1,
        };
    }

    private static async Task<int> RunClassicAsync(string[] args)
    {
        CliOptions options;
        try
        {
            options = CliOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine("Usage: Xm6Control.Cli classic [--address <addr>] [--name WH-1000XM6]");
            return 1;
        }

        Console.WriteLine("Starting Classic RFCOMM Tandem session...");
        var report = await new TandemClassicSession().RunAsync(options.ToRequest());

        WriteReport(Path.Combine("artifacts", "tandem-classic.full.json"), JsonOutput.Serialize(report));
        WriteReport(Path.Combine("docs", "tandem-classic.json"), JsonOutput.SerializeRedacted(report));

        Console.WriteLine($"status: {report.Status}");
        Console.WriteLine($"message: {report.Message}");
        Console.WriteLine($"transport: {report.Transport}");
        Console.WriteLine($"service: {report.ClassicServiceUuid ?? "not found"}");
        Console.WriteLine($"protocolVersion: {report.Handshake.ProtocolVersion?.ToString() ?? "not read"}");
        Console.WriteLine($"tableSetFlag: {report.Handshake.TableSetFlag?.ToString() ?? "not read"}");
        Console.WriteLine($"model: {report.Handshake.Model ?? "not read"}");
        Console.WriteLine($"firmware: {report.Handshake.Firmware ?? "not read"}");
        Console.WriteLine($"supportFunctions: {report.Handshake.SupportFunctions.Count}");
        Console.WriteLine($"frames: {report.Frames.Count}");

        if (report.Warnings.Count > 0)
        {
            Console.WriteLine("warnings:");
            foreach (var warning in report.Warnings)
            {
                Console.WriteLine($"  - {Redaction.RedactMac(warning)}");
            }
        }

        if (report.Errors.Count > 0)
        {
            Console.WriteLine("errors:");
            foreach (var error in report.Errors)
            {
                Console.WriteLine($"  - {Redaction.RedactMac(error)}");
            }
        }

        Console.WriteLine($"redacted report: {Path.GetFullPath(Path.Combine("docs", "tandem-classic.json"))}");
        return report.Status == "ok" ? 0 : 1;
    }

    private static async Task<int> RunTandemAsync(string[] args, bool queryFeatures = false)
    {
        CliOptions options;
        try
        {
            options = CliOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine("Usage: Xm6Control.Cli tandem [--address <addr>] [--name WH-1000XM6]");
            return 1;
        }

        Console.WriteLine(queryFeatures
            ? "Starting Tandem feature query..."
            : "Starting Tandem session...");
        var report = await new TandemGattSession().RunAsync(options.ToRequest(queryFeatures));
        var stem = queryFeatures ? "features-xm6" : "tandem-xm6";

        WriteReport(
            Path.Combine("artifacts", $"{stem}.full.json"),
            JsonOutput.Serialize(report));
        WriteReport(
            Path.Combine("docs", $"{stem}.json"),
            JsonOutput.SerializeRedacted(report));

        Console.WriteLine($"status: {report.Status}");
        Console.WriteLine($"message: {report.Message}");
        Console.WriteLine($"tandemServicePresent: {report.TandemServicePresent}");
        Console.WriteLine($"protocolVersion: {report.Handshake.ProtocolVersion?.ToString() ?? "not read"}");
        Console.WriteLine($"tableSetFlag: {report.Handshake.TableSetFlag?.ToString() ?? "not read"}");
        Console.WriteLine($"model: {report.Handshake.Model ?? "not read"}");
        Console.WriteLine($"firmware: {report.Handshake.Firmware ?? "not read"}");
        Console.WriteLine($"supportFunctions: {report.Handshake.SupportFunctions.Count}");
        Console.WriteLine($"frames: {report.Frames.Count}");

        if (report.FeatureQueries.Count > 0)
        {
            Console.WriteLine("features:");
            foreach (var query in report.FeatureQueries)
            {
                Console.WriteLine(
                    $"  - {query.Name}: {query.Status} type=0x{query.InquiredType:x2} payload={query.PayloadHex ?? "none"}");
                if (query.Detail is not null)
                {
                    Console.WriteLine($"    {query.Detail}");
                }
            }
        }

        if (report.Warnings.Count > 0)
        {
            Console.WriteLine("warnings:");
            foreach (var warning in report.Warnings)
            {
                Console.WriteLine($"  - {Redaction.RedactMac(warning)}");
            }
        }

        if (report.Errors.Count > 0)
        {
            Console.WriteLine("errors:");
            foreach (var error in report.Errors)
            {
                Console.WriteLine($"  - {Redaction.RedactMac(error)}");
            }
        }

        Console.WriteLine($"redacted report: {Path.GetFullPath(Path.Combine("docs", $"{stem}.json"))}");
        Console.WriteLine($"full report: {Path.GetFullPath(Path.Combine("artifacts", $"{stem}.full.json"))}");

        return report.Status == "ok" ? 0 : 1;
    }

    private static async Task<int> RunEqAsync(string[] args)
    {
        CliOptions options;
        try
        {
            options = CliOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine(
                "Usage: Xm6Control.Cli eq [--address <addr>] [--name WH-1000XM6] [--preset <name|0xNN>] [--bands 1,2,...] --yes");
            return 1;
        }

        if (!options.Confirmed)
        {
            Console.Error.WriteLine("EQ changes the headphone state. Re-run with --yes after confirming.");
            return 1;
        }

        Console.WriteLine("Starting Tandem EQ write...");
        var report = await new TandemGattSession().RunAsync(options.ToRequest(applyEq: true));

        WriteReport(
            Path.Combine("artifacts", "eq-xm6.full.json"),
            JsonOutput.Serialize(report));
        WriteReport(
            Path.Combine("docs", "eq-xm6.json"),
            JsonOutput.SerializeRedacted(report));

        Console.WriteLine($"status: {report.Status}");
        Console.WriteLine($"message: {report.Message}");
        Console.WriteLine($"tandemServicePresent: {report.TandemServicePresent}");
        Console.WriteLine($"model: {report.Handshake.Model ?? "not read"}");
        Console.WriteLine($"firmware: {report.Handshake.Firmware ?? "not read"}");

        if (report.Eq is { } eq)
        {
            Console.WriteLine($"eqStatus: {eq.Status}");
            Console.WriteLine($"eqBefore: {FormatEq(eq.Before)}");
            Console.WriteLine($"eqRequested: {FormatEq(eq.Requested)}");
            Console.WriteLine($"eqAfter: {FormatEq(eq.After)}");
            Console.WriteLine($"eqVerified: {eq.Verified}");
            Console.WriteLine($"eqResponse: {eq.ResponseHex ?? "none"}");
        }

        if (report.Warnings.Count > 0)
        {
            Console.WriteLine("warnings:");
            foreach (var warning in report.Warnings)
            {
                Console.WriteLine($"  - {Redaction.RedactMac(warning)}");
            }
        }

        Console.WriteLine($"redacted report: {Path.GetFullPath(Path.Combine("docs", "eq-xm6.json"))}");
        Console.WriteLine($"full report: {Path.GetFullPath(Path.Combine("artifacts", "eq-xm6.full.json"))}");

        return report.Eq?.Verified == true ? 0 : 1;
    }

    private static async Task<int> RunAncAsync(string[] args)
    {
        CliOptions options;
        try
        {
            options = CliOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine(
                "Usage: Xm6Control.Cli anc [--mode nc|asm|off] [--level 1..20] [--noise-adaptive on|off] [--sensitivity standard|high|low] [--voice-focus on|off] [--address <addr>] [--name WH-1000XM6] --yes");
            return 1;
        }

        if (!options.Confirmed)
        {
            Console.Error.WriteLine("ANC/ASM changes the headphone state. Re-run with --yes after confirming.");
            return 1;
        }

        Console.WriteLine("Starting Tandem NC/ASM write...");
        var report = await new TandemGattSession().RunAsync(options.ToRequest(applyNcAsm: true));

        WriteReport(
            Path.Combine("artifacts", "anc-xm6.full.json"),
            JsonOutput.Serialize(report));
        WriteReport(
            Path.Combine("docs", "anc-xm6.json"),
            JsonOutput.SerializeRedacted(report));

        Console.WriteLine($"status: {report.Status}");
        Console.WriteLine($"message: {report.Message}");
        Console.WriteLine($"tandemServicePresent: {report.TandemServicePresent}");
        Console.WriteLine($"model: {report.Handshake.Model ?? "not read"}");
        Console.WriteLine($"firmware: {report.Handshake.Firmware ?? "not read"}");

        if (report.NcAsm is { } ncAsm)
        {
            Console.WriteLine($"ncAsmStatus: {ncAsm.Status}");
            Console.WriteLine($"ncAsmBefore: {FormatNcAsm(ncAsm.Before)}");
            Console.WriteLine($"ncAsmRequested: {FormatNcAsm(ncAsm.Requested)}");
            Console.WriteLine($"ncAsmAfter: {FormatNcAsm(ncAsm.After)}");
            Console.WriteLine($"ncAsmVerified: {ncAsm.Verified}");
            Console.WriteLine($"ncAsmResponse: {ncAsm.ResponseHex ?? "none"}");
        }

        if (report.Warnings.Count > 0)
        {
            Console.WriteLine("warnings:");
            foreach (var warning in report.Warnings)
            {
                Console.WriteLine($"  - {Redaction.RedactMac(warning)}");
            }
        }

        Console.WriteLine($"redacted report: {Path.GetFullPath(Path.Combine("docs", "anc-xm6.json"))}");
        Console.WriteLine($"full report: {Path.GetFullPath(Path.Combine("artifacts", "anc-xm6.full.json"))}");

        return report.NcAsm?.Verified == true ? 0 : 1;
    }

    private static async Task<int> RunMultipointAsync(string[] args)
    {
        CliOptions options;
        try
        {
            options = CliOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine(
                "Usage: Xm6Control.Cli multipoint --multipoint on|off [--address <addr>] [--name WH-1000XM6] --yes");
            return 1;
        }

        if (!options.Confirmed || options.MultipointEnabled is null)
        {
            Console.Error.WriteLine(
                "Multipoint changes the headphone state. Use --multipoint on|off with --yes.");
            return 1;
        }

        Console.WriteLine("Starting Tandem multipoint write...");
        var report = await new TandemGattSession().RunAsync(
            options.ToRequest(applyMultipoint: true));

        WriteReport(
            Path.Combine("artifacts", "multipoint-xm6.full.json"),
            JsonOutput.Serialize(report));
        WriteReport(
            Path.Combine("docs", "multipoint-xm6.json"),
            JsonOutput.SerializeRedacted(report));

        Console.WriteLine($"status: {report.Status}");
        Console.WriteLine($"message: {report.Message}");
        Console.WriteLine($"model: {report.Handshake.Model ?? "not read"}");
        Console.WriteLine($"firmware: {report.Handshake.Firmware ?? "not read"}");

        if (report.Multipoint is { } multipoint)
        {
            Console.WriteLine($"multipointStatus: {multipoint.Status}");
            Console.WriteLine($"multipointBefore: {FormatOnOff(multipoint.Before)}");
            Console.WriteLine($"multipointRequested: {FormatOnOff(multipoint.Requested)}");
            Console.WriteLine($"multipointAfter: {FormatOnOff(multipoint.After)}");
            Console.WriteLine($"multipointVerified: {multipoint.Verified}");
            Console.WriteLine($"multipointResponse: {multipoint.ResponseHex ?? "none"}");
        }

        if (report.Warnings.Count > 0)
        {
            Console.WriteLine("warnings:");
            foreach (var warning in report.Warnings)
            {
                Console.WriteLine($"  - {Redaction.RedactMac(warning)}");
            }
        }

        Console.WriteLine($"redacted report: {Path.GetFullPath(Path.Combine("docs", "multipoint-xm6.json"))}");
        Console.WriteLine($"full report: {Path.GetFullPath(Path.Combine("artifacts", "multipoint-xm6.full.json"))}");

        return report.Multipoint?.Verified == true ? 0 : 1;
    }

    private static async Task<int> RunAutoPowerAsync(string[] args)
    {
        CliOptions options;
        try
        {
            options = CliOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine(
                "Usage: Xm6Control.Cli autopower --autopower off|removed [--address <addr>] [--name WH-1000XM6] --yes");
            return 1;
        }

        if (!options.Confirmed || options.AutoPowerOffElement is null)
        {
            Console.Error.WriteLine(
                "Auto power off changes the headphone state. Use --autopower <value> with --yes.");
            return 1;
        }

        Console.WriteLine("Starting Tandem auto power off write...");
        var report = await new TandemGattSession().RunAsync(
            options.ToRequest(applyAutoPowerOff: true));

        WriteReport(
            Path.Combine("artifacts", "autopower-xm6.full.json"),
            JsonOutput.Serialize(report));
        WriteReport(
            Path.Combine("docs", "autopower-xm6.json"),
            JsonOutput.SerializeRedacted(report));

        Console.WriteLine($"status: {report.Status}");
        Console.WriteLine($"message: {report.Message}");
        Console.WriteLine($"model: {report.Handshake.Model ?? "not read"}");
        Console.WriteLine($"firmware: {report.Handshake.Firmware ?? "not read"}");

        if (report.AutoPowerOff is { } autoPowerOff)
        {
            Console.WriteLine($"autoPowerOffStatus: {autoPowerOff.Status}");
            Console.WriteLine($"autoPowerOffBefore: {FormatAutoPowerOff(autoPowerOff.Before)}");
            Console.WriteLine($"autoPowerOffRequested: {FormatAutoPowerOff(autoPowerOff.Requested)}");
            Console.WriteLine($"autoPowerOffAfter: {FormatAutoPowerOff(autoPowerOff.After)}");
            Console.WriteLine($"autoPowerOffVerified: {autoPowerOff.Verified}");
            Console.WriteLine($"autoPowerOffResponse: {autoPowerOff.ResponseHex ?? "none"}");
            if (autoPowerOff.Elements is { Count: > 0 } elements)
            {
                Console.WriteLine($"autoPowerOffElements: {string.Join(", ", elements)}");
            }
        }

        if (report.Warnings.Count > 0)
        {
            Console.WriteLine("warnings:");
            foreach (var warning in report.Warnings)
            {
                Console.WriteLine($"  - {Redaction.RedactMac(warning)}");
            }
        }

        Console.WriteLine($"redacted report: {Path.GetFullPath(Path.Combine("docs", "autopower-xm6.json"))}");
        Console.WriteLine($"full report: {Path.GetFullPath(Path.Combine("artifacts", "autopower-xm6.full.json"))}");

        return report.AutoPowerOff?.Verified == true ? 0 : 1;
    }

    private static async Task<int> RunListeningAsync(string[] args)
    {
        CliOptions options;
        try
        {
            options = CliOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine(
                "Usage: Xm6Control.Cli listening --listening standard|bgm|cinema [--room small|middle|large] [--address <addr>] [--name WH-1000XM6] --yes");
            return 1;
        }

        if (!options.Confirmed || options.ListeningMode is null)
        {
            Console.Error.WriteLine(
                "Listening mode changes the headphone state. Use --listening standard|bgm|cinema with --yes.");
            return 1;
        }

        Console.WriteLine("Starting Tandem listening mode write...");
        var report = await new TandemGattSession().RunAsync(
            options.ToRequest(applyListeningMode: true));

        WriteReport(
            Path.Combine("artifacts", "listening-xm6.full.json"),
            JsonOutput.Serialize(report));
        WriteReport(
            Path.Combine("docs", "listening-xm6.json"),
            JsonOutput.SerializeRedacted(report));

        Console.WriteLine($"status: {report.Status}");
        Console.WriteLine($"message: {report.Message}");
        Console.WriteLine($"model: {report.Handshake.Model ?? "not read"}");
        Console.WriteLine($"firmware: {report.Handshake.Firmware ?? "not read"}");

        if (report.ListeningMode is { } listening)
        {
            Console.WriteLine($"listeningStatus: {listening.Status}");
            Console.WriteLine($"listeningBefore: {FormatListeningMode(listening.Before)}");
            Console.WriteLine($"listeningRequested: {FormatListeningMode(listening.Requested)}");
            Console.WriteLine($"listeningAfter: {FormatListeningMode(listening.After)}");
            Console.WriteLine($"listeningVerified: {listening.Verified}");
            Console.WriteLine($"listeningResponse: {listening.ResponseHex ?? "none"}");
        }

        if (report.Warnings.Count > 0)
        {
            Console.WriteLine("warnings:");
            foreach (var warning in report.Warnings)
            {
                Console.WriteLine($"  - {Redaction.RedactMac(warning)}");
            }
        }

        Console.WriteLine($"redacted report: {Path.GetFullPath(Path.Combine("docs", "listening-xm6.json"))}");
        Console.WriteLine($"full report: {Path.GetFullPath(Path.Combine("artifacts", "listening-xm6.full.json"))}");

        return report.ListeningMode?.Verified == true ? 0 : 1;
    }

    private static string FormatListeningMode(TandemListeningModeState? state)
    {
        if (state is null)
        {
            return "not read";
        }

        return $"{state.Mode} bgm={state.BgmOnOff} room={state.RoomSize} cinema={state.CinemaOnOff}";
    }

    private static string FormatOnOff(bool? value)
    {
        return value switch
        {
            true => "on",
            false => "off",
            null => "not read",
        };
    }

    private static string FormatAutoPowerOff(TandemAutoPowerOffState? state)
    {
        if (state is null)
        {
            return "not read";
        }

        return $"active={AutoPowerOffName(state.First)}";
    }

    private static string AutoPowerOffName(TandemAutoPowerOffElement element)
    {
        return element switch
        {
            TandemAutoPowerOffElement.WhenRemovedFromEars => "when removed from ears",
            TandemAutoPowerOffElement.Disable => "off",
            _ => "unknown",
        };
    }

    private static string FormatNcAsm(TandemNcAsmState? state)
    {
        return state is null
            ? "not read"
            : $"mode={state.Mode} ambient={state.AmbientMode} level={state.Level} onOff={state.OnOff} noiseAdaptive={state.NoiseAdaptiveOnOff} sensitivity={state.Sensitivity}";
    }

    private static string FormatEq(TandemEqState? state)
    {
        return state is null
            ? "not read"
            : $"{state.Preset} bands={string.Join(',', state.Bands)}";
    }

    private static async Task<int> RunPairAsync(string[] args)
    {
        PairCliOptions options;
        try
        {
            options = PairCliOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine("Usage: Xm6Control.Cli pair [--name LE_WH-1000XM6] [--repair] --yes");
            return 1;
        }

        if (!options.Confirmed)
        {
            Console.Error.WriteLine("Pairing changes Windows Bluetooth state. Re-run with --yes after confirming.");
            return 1;
        }

        Console.WriteLine($"Pairing BLE device '{options.Name}' (repair={options.Repair})...");
        var report = await new BluetoothPairingService().PairAsync(options.Name, options.Repair);
        var output = Path.Combine("docs", "pair-xm6.json");
        WriteReport(output, JsonOutput.SerializeRedacted(report));

        Console.WriteLine($"status: {report.Status}");
        Console.WriteLine($"message: {report.Message}");
        Console.WriteLine($"device: {report.DeviceName}");
        Console.WriteLine($"wasPaired: {report.WasPaired}");
        Console.WriteLine($"unpair: {report.UnpairStatus ?? "not attempted"}");
        Console.WriteLine($"pair: {report.PairStatus ?? "not attempted"}");
        Console.WriteLine($"redacted report: {Path.GetFullPath(output)}");

        return report.Status == "paired" ? 0 : 1;
    }

    private static async Task<int> RunScanAsync(string[] args)
    {
        ScanCliOptions options;
        try
        {
            options = ScanCliOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine("Usage: Xm6Control.Cli scan [--seconds 15] [--output docs/ble-scan.json]");
            return 1;
        }

        if (options.ShowHelp)
        {
            Console.WriteLine("Usage: Xm6Control.Cli scan [--seconds 15] [--output docs/ble-scan.json]");
            return 0;
        }

        Console.WriteLine($"Scanning BLE advertisements for {options.Seconds} seconds...");
        var report = await new BleAdvertisementScanner().ScanAsync(TimeSpan.FromSeconds(options.Seconds));
        WriteReport(options.FullOutput, JsonOutput.Serialize(report));
        WriteReport(options.Output, JsonOutput.SerializeRedacted(report));
        PrintScanSummary(report, options);
        return 0;
    }

    private static void PrintScanSummary(BleScanReport report, ScanCliOptions options)
    {
        Console.WriteLine();
        Console.WriteLine($"advertisements: {report.Advertisements.Count}");
        foreach (var advertisement in report.Advertisements.OrderByDescending(item => item.Rssi))
        {
            var services = advertisement.ServiceUuids.Count == 0
                ? string.Empty
                : $" services={string.Join(',', advertisement.ServiceUuids)}";
            Console.WriteLine(
                $"  - {Redaction.RedactMac(advertisement.Address)} rssi={advertisement.Rssi} name='{advertisement.LocalName ?? string.Empty}' count={advertisement.Count}{services}");
        }

        if (report.Warnings.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("warnings:");
            foreach (var warning in report.Warnings)
            {
                Console.WriteLine($"  - {Redaction.RedactMac(warning)}");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"redacted scan: {Path.GetFullPath(options.Output)}");
    }

    private static void PrintSummary(ProbeReport report, CliOptions options)
    {
        Console.WriteLine();
        Console.WriteLine($"status: {report.Status}");
        if (report.Message is not null)
        {
            Console.WriteLine($"message: {report.Message}");
        }

        Console.WriteLine($"candidates: {report.Candidates.Count}");
        foreach (var candidate in report.Candidates)
        {
            var address = candidate.Address is null
                ? "address=unknown"
                : $"address={Redaction.RedactMac(candidate.Address)}";
            Console.WriteLine(
                $"  - {candidate.Source}: '{candidate.Name}' paired={candidate.IsPaired} connected={candidate.IsConnected} {address}");
        }

        if (report.Gatt is { } gatt)
        {
            Console.WriteLine();
            Console.WriteLine($"GATT: '{gatt.DeviceName}' status={gatt.ConnectionStatus} paired={gatt.IsPaired}");
            Console.WriteLine($"  address: {Redaction.RedactMac(gatt.Address ?? "unknown")}");
            Console.WriteLine($"  services: {gatt.Services.Count}");
            Console.WriteLine($"  characteristics: {gatt.Services.Sum(service => service.Characteristics.Count)}");

            var facts = gatt.Facts;
            Console.WriteLine($"  manufacturer: {facts.Manufacturer ?? "not read"}");
            Console.WriteLine($"  model: {facts.Model ?? "not read"}");
            Console.WriteLine($"  firmware: {facts.Firmware ?? "not read"}");
            Console.WriteLine($"  hardware: {facts.Hardware ?? "not read"}");
            Console.WriteLine($"  software: {facts.Software ?? "not read"}");
            Console.WriteLine($"  serial: {facts.SerialNumber ?? "not read"}");
            Console.WriteLine($"  battery: {facts.BatteryPercent?.ToString() ?? "not read"}");
        }

        if (report.Rfcomm is { } rfcomm)
        {
            Console.WriteLine();
            Console.WriteLine($"RFCOMM: services={rfcomm.Services.Count} connect={rfcomm.ConnectStatus ?? "not attempted"}");
            foreach (var service in rfcomm.Services)
            {
                Console.WriteLine($"  - {service.Name} {service.Uuid} {service.ServiceName}");
            }
        }

        if (report.Warnings.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("warnings:");
            foreach (var warning in report.Warnings)
            {
                Console.WriteLine($"  - {Redaction.RedactMac(warning)}");
            }
        }

        if (report.Errors.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("errors:");
            foreach (var error in report.Errors)
            {
                Console.WriteLine($"  - {Redaction.RedactMac(error)}");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"redacted report: {Path.GetFullPath(options.Output)}");
        Console.WriteLine($"full report:     {Path.GetFullPath(options.FullOutput)}");
    }

    private static void WriteReport(string path, string content)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, content);
    }
}








