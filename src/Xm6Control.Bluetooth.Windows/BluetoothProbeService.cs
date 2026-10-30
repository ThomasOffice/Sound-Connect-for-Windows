using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Storage.Streams;
using Xm6Control.Protocol;

namespace Xm6Control.Bluetooth.Windows;

public sealed class BluetoothProbeService
{
    public async Task<ProbeReport> ProbeAsync(ProbeRequest request, CancellationToken cancellationToken = default)
    {
        var report = new ProbeReport
        {
            RequestedName = request.Name,
            RequestedAddress = request.Address,
            Transport = request.Transport,
        };

        try
        {
            var candidates = await DiscoverCandidatesAsync(report, cancellationToken);
            report.Candidates.AddRange(candidates);
            await AddAdvertisedCandidatesAsync(report, request, cancellationToken);

            if (request.Transport is "auto" or "ble")
            {
                var gatt = await ProbeGattAsync(request, candidates, report, cancellationToken);
                if (gatt is null || gatt.Services.Count == 0 || gatt.ConnectionError is not null)
                {
                    report.Warnings.Add("GATT probe returned no usable services; retrying once.");
                    await Task.Delay(1_000, cancellationToken);
                    gatt = await ProbeGattAsync(request, candidates, report, cancellationToken);
                }

                if (gatt is not null)
                {
                    report.Gatt = gatt;
                    var hasFacts = gatt.Facts.Model is not null
                        || gatt.Facts.Firmware is not null
                        || gatt.Facts.BatteryPercent is not null;
                    report.Status = hasFacts ? "ok" : "gatt_enumerated";
                    report.Message = hasFacts
                        ? "GATT enumerated and at least one device fact was read."
                        : "GATT enumerated, but no model, firmware, or battery value was read.";
                }
            }

            if (report.Gatt is null && request.Transport is "auto" or "rfcomm")
            {
                var rfcomm = await RfcommProbeService.ProbeAsync(request, report, cancellationToken);
                if (rfcomm is not null)
                {
                    report.Rfcomm = rfcomm;
                    report.Status = "rfcomm_found";
                    report.Message = "GATT was not available; RFCOMM services were enumerated.";
                }
            }

            if (report.Gatt is null && report.Rfcomm is null)
            {
                report.Status = "not_found";
                report.Message = "No GATT or RFCOMM target was found.";
            }
        }
        catch (OperationCanceledException)
        {
            report.Status = "cancelled";
            report.Message = "Probe was cancelled.";
        }
        catch (Exception ex)
        {
            report.Status = "error";
            report.Message = "Probe failed.";
            report.Errors.Add($"{ex.GetType().Name}: {ex.Message}");
        }

        report.CompletedAt = DateTimeOffset.Now;
        return report;
    }

    private static async Task<List<DeviceCandidate>> DiscoverCandidatesAsync(
        ProbeReport report,
        CancellationToken cancellationToken)
    {
        var candidates = new List<DeviceCandidate>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var devices = await DeviceInformation.FindAllAsync(BluetoothLEDevice.GetDeviceSelector());
            foreach (var device in devices)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AddCandidate(device, "ble", candidates, seen);
            }
        }
        catch (Exception ex)
        {
            report.Warnings.Add($"BLE enumeration failed: {ex.GetType().Name}: {ex.Message}");
        }

        try
        {
            var devices = await DeviceInformation.FindAllAsync(BluetoothDevice.GetDeviceSelectorFromPairingState(true));
            foreach (var device in devices)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AddCandidate(device, "classic", candidates, seen);
            }
        }
        catch (Exception ex)
        {
            report.Warnings.Add($"Classic Bluetooth enumeration failed: {ex.GetType().Name}: {ex.Message}");
        }

        return candidates;
    }

    private static async Task AddAdvertisedCandidatesAsync(
        ProbeReport report,
        ProbeRequest request,
        CancellationToken cancellationToken)
    {
        var hasAddress = report.Candidates.Any(candidate =>
            Matches(candidate, request)
            && BluetoothAddress.TryParse(candidate.Address ?? candidate.Id, out _));
        if (hasAddress)
        {
            return;
        }

        var scan = await new BleAdvertisementScanner().ScanAsync(TimeSpan.FromSeconds(5), cancellationToken);
        foreach (var advertisement in scan.Advertisements)
        {
            if (!NameMatches(advertisement.LocalName, request.Name))
            {
                continue;
            }

            var normalized = BluetoothAddress.Normalize(advertisement.Address);
            var duplicate = report.Candidates.Any(candidate =>
                BluetoothAddress.Normalize(candidate.Address) == normalized);
            if (duplicate)
            {
                continue;
            }

            report.Candidates.Add(new DeviceCandidate
            {
                Name = advertisement.LocalName ?? request.Name,
                Source = "ble-advertisement",
                Id = $"advertisement:{advertisement.Address}",
                Address = advertisement.Address,
                IsPaired = false,
                IsConnected = false,
            });
        }
    }

    private static void AddCandidate(
        DeviceInformation device,
        string source,
        List<DeviceCandidate> candidates,
        HashSet<string> seen)
    {
        if (string.IsNullOrWhiteSpace(device.Id) || !seen.Add(device.Id))
        {
            return;
        }

        candidates.Add(new DeviceCandidate
        {
            Name = device.Name ?? string.Empty,
            Source = source,
            Id = device.Id,
            Address = GetStringProperty(device, "System.Devices.Aep.DeviceAddress"),
            IsPaired = SafeIsPaired(device),
            IsConnected = GetBoolProperty(device, "System.Devices.Aep.IsConnected"),
        });
    }

    private static async Task<GattProbeResult?> ProbeGattAsync(
        ProbeRequest request,
        IReadOnlyList<DeviceCandidate> candidates,
        ProbeReport report,
        CancellationToken cancellationToken)
    {
        var target = FindTarget(request, candidates, report);
        var attempts = new List<(ulong Address, bool RequireNameMatch)>();
        BluetoothLEDevice? device = null;

        if (BluetoothAddress.TryParse(request.Address, out var requestedAddress))
        {
            attempts.Add((requestedAddress, false));
        }

        if (target is not null)
        {
            if (BluetoothAddress.TryParse(target.Address, out var targetAddress))
            {
                attempts.Add((targetAddress, false));
            }

            if (BluetoothAddress.TryParse(target.Id, out var idAddress))
            {
                attempts.Add((idAddress, false));
            }

            if (target.Source == "classic")
            {
                try
                {
                    var classic = await BluetoothDevice.FromIdAsync(target.Id);
                    if (classic is not null)
                    {
                        attempts.Add((classic.BluetoothAddress, false));
                    }
                }
                catch (Exception ex)
                {
                    report.Warnings.Add($"Classic-to-BLE bridge failed: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        var primaryAddresses = attempts.Select(attempt => attempt.Address).Distinct().ToList();
        foreach (var address in primaryAddresses)
        {
            attempts.Add((address + 1, true));
            attempts.Add((address - 1, true));
        }

        foreach (var attempt in attempts.Distinct())
        {
            if (attempt.Address == 0)
            {
                continue;
            }

            var addressType = request.AddressType switch
            {
                "public" => BluetoothAddressType.Public,
                "random" => BluetoothAddressType.Random,
                _ => BluetoothAddressType.Unspecified,
            };
            var candidate = await BluetoothLEDevice.FromBluetoothAddressAsync(attempt.Address, addressType);
            if (candidate is null)
            {
                continue;
            }

            if (attempt.RequireNameMatch
                && target is not null
                && !NameMatches(candidate.Name, target.Name))
            {
                candidate.Dispose();
                continue;
            }

            device = candidate;
            break;
        }

        if (device is null && target is not null && target.Source == "ble")
        {
            try
            {
                device = await BluetoothLEDevice.FromIdAsync(target.Id);
            }
            catch (Exception ex)
            {
                report.Warnings.Add($"BLE FromIdAsync failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        if (device is null)
        {
            report.Warnings.Add(
                $"No BLE device could be opened for the requested target; tried {attempts.Distinct().Count()} address(es).");
            return null;
        }

        using (device)
        {
            var result = new GattProbeResult
            {
                DeviceName = string.IsNullOrWhiteSpace(device.Name) ? target?.Name ?? request.Name : device.Name,
                DeviceId = device.DeviceId,
                Address = BluetoothAddress.Format(device.BluetoothAddress),
                IsPaired = SafeIsPaired(device.DeviceInformation),
                ConnectionStatus = device.ConnectionStatus.ToString(),
            };

            try
            {
                using var session = await GattSession.FromDeviceIdAsync(device.BluetoothDeviceId);
                session.MaintainConnection = true;
                for (var attempt = 0; attempt < 25 && session.SessionStatus != GattSessionStatus.Active; attempt++)
                {
                    await Task.Delay(200, cancellationToken);
                }

                if (session.SessionStatus != GattSessionStatus.Active)
                {
                    report.Warnings.Add($"GATT session did not become active: {session.SessionStatus}.");
                }

                var servicesResult = await device.GetGattServicesAsync(BluetoothCacheMode.Uncached);
                if (servicesResult.Status != GattCommunicationStatus.Success)
                {
                    result.ConnectionError = servicesResult.Status.ToString();
                    report.Warnings.Add($"GATT service discovery failed: {servicesResult.Status}.");
                    return result;
                }

                var services = servicesResult.Services;
                var subscriptions = new List<Subscription>();

                foreach (var service in services)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var serviceInfo = new GattServiceInfo
                    {
                        Uuid = service.Uuid.ToString("D"),
                        Name = GattUuidNames.GetName(service.Uuid),
                    };
                    result.Services.Add(serviceInfo);

                    try
                    {
                        var characteristicsResult = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                        if (characteristicsResult.Status != GattCommunicationStatus.Success)
                        {
                            report.Warnings.Add(
                                $"Characteristic discovery failed for {serviceInfo.Uuid}: {characteristicsResult.Status}.");
                            continue;
                        }

                        foreach (var characteristic in characteristicsResult.Characteristics)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            var characteristicInfo = new GattCharacteristicInfo
                            {
                                Uuid = characteristic.Uuid.ToString("D"),
                                Name = GattUuidNames.GetName(characteristic.Uuid),
                                Properties = characteristic.CharacteristicProperties.ToString(),
                            };
                            serviceInfo.Characteristics.Add(characteristicInfo);

                            await ReadCharacteristicAsync(characteristic, characteristicInfo, result.Facts, cancellationToken);
                            await SubscribeAsync(characteristic, characteristicInfo, subscriptions, report);
                        }
                    }
                    catch (Exception ex)
                    {
                        report.Warnings.Add(
                            $"GATT service {serviceInfo.Uuid} failed: {ex.GetType().Name}: {ex.Message}");
                    }
                }

                if (subscriptions.Count > 0 && request.NotificationWaitMs > 0)
                {
                    await Task.Delay(request.NotificationWaitMs, cancellationToken);
                }

                foreach (var subscription in subscriptions)
                {
                    subscription.Characteristic.ValueChanged -= subscription.Handler;
                    try
                    {
                        await subscription.Characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                            GattClientCharacteristicConfigurationDescriptorValue.None);
                    }
                    catch
                    {
                        // Best-effort cleanup; the probe result is still useful.
                    }
                }

                foreach (var service in services)
                {
                    service.Dispose();
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                result.ConnectionError = $"{ex.GetType().Name}: {ex.Message}";
                report.Errors.Add($"GATT probe failed: {ex.GetType().Name}: {ex.Message}");
            }

            return result;
        }
    }

    private static async Task ReadCharacteristicAsync(
        GattCharacteristic characteristic,
        GattCharacteristicInfo info,
        GattDeviceFacts facts,
        CancellationToken cancellationToken)
    {
        if (!characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Read))
        {
            return;
        }

        try
        {
            var readResult = await characteristic.ReadValueAsync(BluetoothCacheMode.Uncached);
            info.ReadStatus = readResult.Status.ToString();
            if (readResult.Status != GattCommunicationStatus.Success || readResult.Value is null)
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var bytes = ReadBuffer(readResult.Value);
            info.ValueLength = bytes.Length;
            info.ValueHex = ByteFormatting.ToHex(bytes.AsSpan(0, Math.Min(bytes.Length, 64)));

            var text = ByteFormatting.Describe(bytes);
            if (GattUuidNames.Is(characteristic.Uuid, 0x2A25))
            {
                info.ValueText = "[redacted]";
                facts.SerialNumber = "[redacted]";
                return;
            }

            info.ValueText = text;
            ApplyFact(characteristic.Uuid, bytes, text, facts);
        }
        catch (Exception ex)
        {
            info.ReadStatus = $"{ex.GetType().Name}: {ex.Message}";
        }
    }

    private static void ApplyFact(Guid uuid, byte[] bytes, string text, GattDeviceFacts facts)
    {
        if (GattUuidNames.Is(uuid, 0x2A29))
        {
            facts.Manufacturer = text;
        }
        else if (GattUuidNames.Is(uuid, 0x2A24))
        {
            facts.Model = text;
        }
        else if (GattUuidNames.Is(uuid, 0x2A26))
        {
            facts.Firmware = text;
        }
        else if (GattUuidNames.Is(uuid, 0x2A27))
        {
            facts.Hardware = text;
        }
        else if (GattUuidNames.Is(uuid, 0x2A28))
        {
            facts.Software = text;
        }
        else if (GattUuidNames.Is(uuid, 0x2A19) && bytes.Length > 0)
        {
            facts.BatteryPercent = bytes[0];
        }
    }

    private static async Task SubscribeAsync(
        GattCharacteristic characteristic,
        GattCharacteristicInfo info,
        List<Subscription> subscriptions,
        ProbeReport report)
    {
        var properties = characteristic.CharacteristicProperties;
        var cccdValue = properties.HasFlag(GattCharacteristicProperties.Notify)
            ? GattClientCharacteristicConfigurationDescriptorValue.Notify
            : properties.HasFlag(GattCharacteristicProperties.Indicate)
                ? GattClientCharacteristicConfigurationDescriptorValue.Indicate
                : GattClientCharacteristicConfigurationDescriptorValue.None;

        if (cccdValue == GattClientCharacteristicConfigurationDescriptorValue.None)
        {
            return;
        }

        TypedEventHandler<GattCharacteristic, GattValueChangedEventArgs> handler = (_, args) =>
        {
            try
            {
                var bytes = ReadBuffer(args.CharacteristicValue);
                lock (info.Notifications)
                {
                    info.NotificationCount++;
                    if (info.Notifications.Count < 8)
                    {
                        info.Notifications.Add(
                            ByteFormatting.ToHex(bytes.AsSpan(0, Math.Min(bytes.Length, 64))));
                    }
                }
            }
            catch
            {
                // A malformed notification must not terminate the probe.
            }
        };

        try
        {
            var status = await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(cccdValue);
            info.CccdStatus = status.ToString();
            if (status == GattCommunicationStatus.Success)
            {
                characteristic.ValueChanged += handler;
                subscriptions.Add(new Subscription(characteristic, handler, info));
            }
            else
            {
                report.Warnings.Add($"CCCD write failed for {info.Uuid}: {status}.");
            }
        }
        catch (Exception ex)
        {
            info.CccdStatus = $"{ex.GetType().Name}: {ex.Message}";
            report.Warnings.Add($"CCCD write threw for {info.Uuid}: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static byte[] ReadBuffer(IBuffer buffer)
    {
        var bytes = new byte[(int)buffer.Length];
        using var reader = DataReader.FromBuffer(buffer);
        reader.ReadBytes(bytes);
        return bytes;
    }

    private static DeviceCandidate? FindTarget(
        ProbeRequest request,
        IReadOnlyList<DeviceCandidate> candidates,
        ProbeReport report)
    {
        var matches = candidates
            .Where(candidate => Matches(candidate, request))
            .OrderByDescending(candidate => BluetoothAddress.TryParse(candidate.Address ?? candidate.Id, out _))
            .ThenByDescending(candidate => candidate.Source == "ble-advertisement")
            .ToList();

        if (matches.Count == 0)
        {
            return null;
        }

        if (matches.Count > 1)
        {
            report.Warnings.Add(
                $"Multiple candidates matched '{request.Name}'; using {matches[0].Source} '{matches[0].Name}'.");
        }

        return matches[0];
    }

    private static bool NameMatches(string? actual, string? requested)
    {
        return !string.IsNullOrWhiteSpace(actual)
            && !string.IsNullOrWhiteSpace(requested)
            && actual.Contains(requested, StringComparison.OrdinalIgnoreCase);
    }

    private static bool Matches(DeviceCandidate candidate, ProbeRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Address))
        {
            var requested = BluetoothAddress.Normalize(request.Address);
            var actual = BluetoothAddress.Normalize(candidate.Address);
            if (requested is not null && actual is not null)
            {
                return requested == actual;
            }
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return false;
        }

        return candidate.Name.Contains(request.Name, StringComparison.OrdinalIgnoreCase)
            || candidate.Id.Contains(request.Name, StringComparison.OrdinalIgnoreCase);
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

    private static string? GetStringProperty(DeviceInformation device, string key)
    {
        return device.Properties.TryGetValue(key, out var value)
            ? value?.ToString()
            : null;
    }

    private static bool GetBoolProperty(DeviceInformation device, string key)
    {
        return device.Properties.TryGetValue(key, out var value)
            && value is bool boolean
            && boolean;
    }

    private sealed record Subscription(
        GattCharacteristic Characteristic,
        TypedEventHandler<GattCharacteristic, GattValueChangedEventArgs> Handler,
        GattCharacteristicInfo Info);
}
