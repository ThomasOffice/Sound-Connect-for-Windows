using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;

namespace Xm6Control.Bluetooth.Windows;

internal static class RfcommProbeService
{
    public static async Task<RfcommProbeResult?> ProbeAsync(
        ProbeRequest request,
        ProbeReport report,
        CancellationToken cancellationToken)
    {
        var result = new RfcommProbeResult();

        try
        {
            var selector = RfcommDeviceService.GetDeviceSelector(RfcommServiceId.SerialPort);
            var devices = await DeviceInformation.FindAllAsync(selector);

            foreach (var device in devices)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!Matches(device, request))
                {
                    continue;
                }

                var info = new RfcommServiceInfo
                {
                    Name = device.Name ?? string.Empty,
                    Id = device.Id,
                    Uuid = RfcommServiceId.SerialPort.Uuid.ToString("D"),
                };
                result.Services.Add(info);

                try
                {
                    var service = await RfcommDeviceService.FromIdAsync(device.Id);
                    if (service is not null)
                    {
                        info.ServiceName = service.ConnectionServiceName;
                        info.Uuid = service.ServiceId.Uuid.ToString("D");
                        result.ConnectStatus = "service_opened";
                        service.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    result.ConnectStatus = $"{ex.GetType().Name}: {ex.Message}";
                    report.Warnings.Add(
                        $"RFCOMM service open failed for '{device.Name}': {ex.GetType().Name}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            report.Warnings.Add($"RFCOMM enumeration failed: {ex.GetType().Name}: {ex.Message}");
        }

        return result.Services.Count > 0 ? result : null;
    }

    private static bool Matches(DeviceInformation device, ProbeRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Address))
        {
            var address = device.Properties.TryGetValue("System.Devices.Aep.DeviceAddress", out var value)
                ? value?.ToString()
                : null;
            var requested = BluetoothAddress.Normalize(request.Address);
            var actual = BluetoothAddress.Normalize(address);
            if (requested is not null && actual is not null)
            {
                return requested == actual;
            }
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return false;
        }

        return (device.Name ?? string.Empty).Contains(request.Name, StringComparison.OrdinalIgnoreCase)
            || device.Id.Contains(request.Name, StringComparison.OrdinalIgnoreCase);
    }
}
