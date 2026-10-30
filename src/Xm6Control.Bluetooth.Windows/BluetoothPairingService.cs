using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace Xm6Control.Bluetooth.Windows;

public sealed class BluetoothPairingService
{
    public async Task<PairingReport> PairAsync(
        string name,
        bool repair,
        CancellationToken cancellationToken = default)
    {
        var report = new PairingReport { RequestedName = name, Repair = repair };
        BluetoothLEDevice? scannedDevice = null;

        var candidates = new List<DeviceInformation>();
        foreach (var selector in new[]
                 {
                     BluetoothLEDevice.GetDeviceSelectorFromPairingState(true),
                     BluetoothLEDevice.GetDeviceSelector(),
                 })
        {
            foreach (var device in await DeviceInformation.FindAllAsync(selector))
            {
                if (candidates.All(existing => existing.Id != device.Id))
                {
                    candidates.Add(device);
                }
            }
        }

        var target = candidates
            .Where(device => (device.Name ?? string.Empty).Contains(name, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(SafeIsPaired)
            .FirstOrDefault();

        if (target is null)
        {
            var scan = await new BleAdvertisementScanner().ScanAsync(TimeSpan.FromSeconds(5), cancellationToken);
            var advertisement = scan.Advertisements.FirstOrDefault(item =>
                !string.IsNullOrWhiteSpace(item.LocalName)
                && item.LocalName.Contains(name, StringComparison.OrdinalIgnoreCase));

            if (advertisement is not null
                && BluetoothAddress.TryParse(advertisement.Address, out var address))
            {
                scannedDevice = await BluetoothLEDevice.FromBluetoothAddressAsync(
                    address,
                    BluetoothAddressType.Random);
                target = scannedDevice?.DeviceInformation;
            }
        }

        if (target is null)
        {
            report.Status = "not_found";
            report.Message = $"No BLE device matched '{name}'.";
            scannedDevice?.Dispose();
            return report;
        }

        report.DeviceName = target.Name;
        report.DeviceId = target.Id;
        report.WasPaired = SafeIsPaired(target);

        if (repair)
        {
            try
            {
                var unpair = await target.Pairing.UnpairAsync();
                report.UnpairStatus = unpair.Status.ToString();
                await Task.Delay(1_000, cancellationToken);

                var refreshed = await DeviceInformation.FindAllAsync(
                    BluetoothLEDevice.GetDeviceSelectorFromPairingState(true));
                var refreshedTarget = refreshed.FirstOrDefault(device =>
                    (device.Name ?? string.Empty).Contains(name, StringComparison.OrdinalIgnoreCase));
                if (refreshedTarget is not null)
                {
                    target = refreshedTarget;
                }
            }
            catch (Exception ex)
            {
                report.UnpairStatus = $"{ex.GetType().Name}: {ex.Message}";
            }
        }

        foreach (var level in new[]
                 {
                     DevicePairingProtectionLevel.Encryption,
                     DevicePairingProtectionLevel.Default,
                     DevicePairingProtectionLevel.None,
                 })
        {
            try
            {
                var result = await target.Pairing.PairAsync(level);
                report.PairAttempts.Add($"{level}={result.Status}");
                report.PairStatus = result.Status.ToString();
                if (result.Status is DevicePairingResultStatus.Paired
                    or DevicePairingResultStatus.AlreadyPaired)
                {
                    report.Status = "paired";
                    report.Message = "BLE device is paired.";
                    break;
                }
            }
            catch (Exception ex)
            {
                report.PairAttempts.Add($"{level}={ex.GetType().Name}: {ex.Message}");
            }
        }

        if (report.Status != "paired")
        {
            try
            {
                var custom = target.Pairing.Custom;
                custom.PairingRequested += (_, args) =>
                {
                    if (args.PairingKind == DevicePairingKinds.ConfirmOnly)
                    {
                        args.Accept();
                    }
                    else if (args.PairingKind == DevicePairingKinds.ProvidePin)
                    {
                        args.Accept("0000");
                    }
                };

                var result = await custom.PairAsync(
                    DevicePairingKinds.ConfirmOnly | DevicePairingKinds.ProvidePin,
                    DevicePairingProtectionLevel.Default);
                report.PairAttempts.Add($"custom={result.Status}");
                report.PairStatus = result.Status.ToString();
                if (result.Status is DevicePairingResultStatus.Paired
                    or DevicePairingResultStatus.AlreadyPaired)
                {
                    report.Status = "paired";
                    report.Message = "BLE device is paired.";
                }
            }
            catch (Exception ex)
            {
                report.PairAttempts.Add($"custom={ex.GetType().Name}: {ex.Message}");
            }
        }

        if (report.Status != "paired")
        {
            report.Status = "pair_failed";
            report.Message = $"Pairing failed: {string.Join("; ", report.PairAttempts)}";
        }

        scannedDevice?.Dispose();
        cancellationToken.ThrowIfCancellationRequested();
        report.CompletedAt = DateTimeOffset.Now;
        return report;
    }

    private static bool SafeIsPaired(DeviceInformation device)
    {
        try
        {
            return device.Pairing?.IsPaired == true;
        }
        catch
        {
            return false;
        }
    }
}

public sealed class PairingReport
{
    public DateTimeOffset CompletedAt { get; set; }
    public string RequestedName { get; set; } = string.Empty;
    public string? DeviceName { get; set; }
    public string? DeviceId { get; set; }
    public bool Repair { get; set; }
    public bool WasPaired { get; set; }
    public string? UnpairStatus { get; set; }
    public string? PairStatus { get; set; }
    public List<string> PairAttempts { get; set; } = [];
    public string Status { get; set; } = "unknown";
    public string? Message { get; set; }
}


