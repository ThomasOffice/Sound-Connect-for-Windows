using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Foundation;

namespace Xm6Control.Bluetooth.Windows;

public sealed class BleAdvertisementScanner
{
    public async Task<BleScanReport> ScanAsync(TimeSpan duration, CancellationToken cancellationToken = default)
    {
        var report = new BleScanReport
        {
            StartedAt = DateTimeOffset.Now,
            DurationSeconds = duration.TotalSeconds,
        };

        var watcher = new BluetoothLEAdvertisementWatcher
        {
            ScanningMode = BluetoothLEScanningMode.Active,
        };

        watcher.Received += (_, args) => OnAdvertisement(report, args);
        watcher.Stopped += (_, args) =>
        {
            if (args.Error != BluetoothError.Success)
            {
                lock (report.Warnings)
                {
                    report.Warnings.Add($"Watcher stopped: {args.Error}");
                }
            }
        };

        try
        {
            watcher.Start();
            await Task.Delay(duration, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Stop below and return what was observed.
        }
        finally
        {
            watcher.Stop();
        }

        report.CompletedAt = DateTimeOffset.Now;
        return report;
    }

    private static void OnAdvertisement(BleScanReport report, BluetoothLEAdvertisementReceivedEventArgs args)
    {
        try
        {
            var address = BluetoothAddress.Format(args.BluetoothAddress);
            var name = args.Advertisement?.LocalName;
            var serviceUuids = args.Advertisement?.ServiceUuids?
                .Select(uuid => uuid.ToString("D"))
                .ToList() ?? [];

            lock (report.Advertisements)
            {
                var existing = report.Advertisements.FirstOrDefault(item =>
                    string.Equals(item.Address, address, StringComparison.OrdinalIgnoreCase));
                if (existing is null)
                {
                    report.Advertisements.Add(new BleAdvertisementInfo
                    {
                        Address = address,
                        LocalName = name,
                        Rssi = args.RawSignalStrengthInDBm,
                        AdvertisementType = args.AdvertisementType.ToString(),
                        ServiceUuids = serviceUuids,
                        Count = 1,
                        LastSeen = DateTimeOffset.Now,
                    });
                }
                else
                {
                    existing.Count++;
                    existing.LastSeen = DateTimeOffset.Now;
                    existing.Rssi = args.RawSignalStrengthInDBm;
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        existing.LocalName = name;
                    }

                    foreach (var uuid in serviceUuids)
                    {
                        if (!existing.ServiceUuids.Contains(uuid, StringComparer.OrdinalIgnoreCase))
                        {
                            existing.ServiceUuids.Add(uuid);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            lock (report.Warnings)
            {
                report.Warnings.Add($"Advertisement handler failed: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}

public sealed class BleScanReport
{
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
    public double DurationSeconds { get; set; }
    public List<BleAdvertisementInfo> Advertisements { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}

public sealed class BleAdvertisementInfo
{
    public string Address { get; set; } = string.Empty;
    public string? LocalName { get; set; }
    public short Rssi { get; set; }
    public string AdvertisementType { get; set; } = string.Empty;
    public List<string> ServiceUuids { get; set; } = [];
    public int Count { get; set; }
    public DateTimeOffset LastSeen { get; set; }
}
