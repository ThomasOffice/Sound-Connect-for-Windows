using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Storage.Streams;
using Xm6Control.Protocol;

namespace Xm6Control.Bluetooth.Windows;

public sealed class TandemGattSession
{
    private static readonly Guid TandemServiceUuid = Guid.Parse("5b833e20-6bc7-4802-8e9a-723ceca4bd8f");
    private static readonly Guid PairingCompleteNameServiceUuid = Guid.Parse("5b833e26-6bc7-4802-8e9a-723ceca4bd8f");
    private static readonly Guid BluetoothModeUuid = Guid.Parse("5b833c11-6bc7-4802-8e9a-723ceca4bd8f");
    private static readonly Guid BluetoothModeStatusUuid = Guid.Parse("5b833c13-6bc7-4802-8e9a-723ceca4bd8f");
    private static readonly Guid CompleteNameUuid = Guid.Parse("5b833c1b-6bc7-4802-8e9a-723ceca4bd8f");
    private static readonly Guid SyncNameIndexUuid = Guid.Parse("5b833c1c-6bc7-4802-8e9a-723ceca4bd8f");
    private static readonly Guid ToAccUuid = Guid.Parse("5b833c60-6bc7-4802-8e9a-723ceca4bd8f");
    private static readonly Guid FromAccUuid = Guid.Parse("5b833c61-6bc7-4802-8e9a-723ceca4bd8f");
    private static readonly Guid WritableLengthUuid = Guid.Parse("5b833c91-6bc7-4802-8e9a-723ceca4bd8f");
    private static readonly Guid DetermineMtuUuid = Guid.Parse("5b833c93-6bc7-4802-8e9a-723ceca4bd8f");

    private readonly object _frameGate = new();
    private readonly List<byte> _receiveBuffer = [];
    private readonly object _pendingGate = new();
    private TaskCompletionSource<TandemFrame>? _pending;
    private TaskCompletionSource<byte>? _ackPending;
    private HashSet<byte>? _pendingCommands;
    private TaskCompletionSource<byte[]>? _mtuPending;
    private BluetoothLEDevice? _device;
    private GattDeviceService? _service;
    private GattCharacteristic? _toAcc;
    private GattCharacteristic? _fromAcc;
    private byte _sequence;
    private TandemSessionReport? _report;
    private TandemGsInquiredType? _multipointGsType;
    private TandemAutoPowerOffCapability? _autoPowerOffCapability;
    private TandemAudioInquiredType? _bgmAudioType;
    private CinemaAudioPath? _cinemaAudioPath;

    private enum CinemaAudioPath
    {
        None,
        UpmixCinema,
        UpmixSeries,
    }

    public async Task<TandemSessionReport> RunAsync(
        ProbeRequest request,
        CancellationToken cancellationToken = default)
    {
        var report = new TandemSessionReport
        {
            StartedAt = DateTimeOffset.Now,
        };
        _report = report;

        try
        {
            _device = await OpenDeviceAsync(request, report, cancellationToken);
            if (_device is null)
            {
                report.Status = "not_found";
                report.Message = "No BLE device could be opened.";
                return report;
            }

            report.DeviceName = _device.Name;
            report.DeviceId = _device.DeviceId;
            report.Address = BluetoothAddress.Format(_device.BluetoothAddress);

            using var session = await GattSession.FromDeviceIdAsync(_device.BluetoothDeviceId);
            session.MaintainConnection = true;
            for (var attempt = 0; attempt < 25 && session.SessionStatus != GattSessionStatus.Active; attempt++)
            {
                await Task.Delay(200, cancellationToken);
            }

            if (session.SessionStatus != GattSessionStatus.Active)
            {
                report.Warnings.Add($"GATT session did not become active: {session.SessionStatus}.");
            }

            GattDeviceServicesResult? servicesResult = null;
            for (var attempt = 0; attempt < 2; attempt++)
            {
                servicesResult = await _device.GetGattServicesAsync(BluetoothCacheMode.Uncached);
                if (servicesResult.Status == GattCommunicationStatus.Success
                    && servicesResult.Services.Count > 0)
                {
                    break;
                }

                if (attempt == 0)
                {
                    report.Warnings.Add(
                        $"GATT service discovery returned {servicesResult.Status}; retrying once.");
                    await Task.Delay(1_000, cancellationToken);
                }
            }

            if (servicesResult is null
                || servicesResult.Status != GattCommunicationStatus.Success
                || servicesResult.Services.Count == 0)
            {
                report.Status = "gatt_unreachable";
                report.Message =
                    $"GATT service discovery failed: {servicesResult?.Status.ToString() ?? "no result"}.";
                return report;
            }

            foreach (var service in servicesResult.Services)
            {
                var brief = new GattServiceBrief
                {
                    Uuid = service.Uuid.ToString("D"),
                    Name = GattUuidNames.GetName(service.Uuid),
                };

                try
                {
                    var characteristics = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                    brief.CharacteristicCount = characteristics.Characteristics.Count;
                }
                catch
                {
                    // Keep the service in the report even if character enumeration fails.
                }

                report.Services.Add(brief);
            }

            _service = servicesResult.Services.FirstOrDefault(service => service.Uuid == TandemServiceUuid);
            if (_service is null && request.BootstrapTandem)
            {
                await TryBootstrapTandemAsync(servicesResult.Services, report, cancellationToken);
                await Task.Delay(5_000, cancellationToken);
                var refreshed = await _device.GetGattServicesAsync(BluetoothCacheMode.Uncached);
                if (refreshed.Status == GattCommunicationStatus.Success)
                {
                    _service = refreshed.Services.FirstOrDefault(service => service.Uuid == TandemServiceUuid);
                }
            }

            report.TandemServicePresent = _service is not null;
            if (_service is null)
            {
                report.Status = "tandem_service_missing";
                report.Message =
                    "Tandem V2 HPC service 5b833e20 is not exposed by the headphone in this state. "
                    + "The Android app likely holds the Tandem connection, or Windows must be the active audio host. "
                    + "Disconnect the phone / close Sound Connect, connect WH-1000XM6 to Windows audio, then retry.";
                if (request.BootstrapTandem)
                {
                    report.Message += $" Pairing bootstrap result: {report.PairingBootstrapResult ?? "not attempted"}.";
                }

                return report;
            }

            var characteristicsResult = await _service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
            if (characteristicsResult.Status != GattCommunicationStatus.Success)
            {
                report.Status = "tandem_service_unreadable";
                report.Message = $"Tandem characteristic discovery failed: {characteristicsResult.Status}.";
                return report;
            }

            _toAcc = Find(characteristicsResult, ToAccUuid);
            _fromAcc = Find(characteristicsResult, FromAccUuid);
            var writableLength = Find(characteristicsResult, WritableLengthUuid);
            var determineMtu = Find(characteristicsResult, DetermineMtuUuid);

            if (_toAcc is null || _fromAcc is null)
            {
                report.Status = "tandem_characteristic_missing";
                report.Message = "Tandem TO_ACC or FROM_ACC characteristic is missing.";
                return report;
            }

            if (determineMtu is not null)
            {
                await ObserveMtuAsync(determineMtu, report, cancellationToken);
            }

            if (writableLength is not null)
            {
                var length = await ReadAsync(writableLength, cancellationToken);
                if (length is { Length: > 0 })
                {
                    report.Handshake.WritableValueLength = length[0];
                }
            }

            _fromAcc.ValueChanged += OnFromAccValueChanged;
            var notifyStatus = await _fromAcc.WriteClientCharacteristicConfigurationDescriptorAsync(
                GattClientCharacteristicConfigurationDescriptorValue.Notify);
            if (notifyStatus != GattCommunicationStatus.Success)
            {
                report.Status = "tandem_notify_failed";
                report.Message = $"Could not subscribe to FROM_ACC: {notifyStatus}.";
                return report;
            }

            var protocol = await SendAndWaitAsync(
                TandemV2Table1Messages.ConnectGetProtocolInfo(),
                [(byte)TandemV2Table1Command.ConnectRetProtocolInfo],
                report,
                cancellationToken);
            if (protocol is not null
                && TandemV2Table1Messages.TryParseProtocolInfo(
                    protocol.Payload,
                    out var protocolVersion,
                    out var tableSetFlag,
                    out var secondFlag))
            {
                report.Handshake.ProtocolVersion = protocolVersion;
                report.Handshake.TableSetFlag = tableSetFlag;
                report.Handshake.SecondFlag = secondFlag;
            }

            var capability = await SendAndWaitAsync(
                TandemV2Table1Messages.ConnectGetCapabilityInfo(),
                [(byte)TandemV2Table1Command.ConnectRetCapabilityInfo],
                report,
                cancellationToken);
            if (capability is not null)
            {
                report.Handshake.CapabilityPayloadHex = Convert.ToHexString(capability.Payload).ToLowerInvariant();
            }

            var support = await SendAndWaitAsync(
                TandemV2Table1Messages.ConnectGetSupportFunction(),
                [(byte)TandemV2Table1Command.ConnectRetSupportFunction],
                report,
                cancellationToken);
            if (support is not null
                && TandemV2Table1Messages.TryParseSupportFunctions(
                    support.Payload,
                    out var functions))
            {
                report.Handshake.SupportFunctions = functions
                    .Select(function => new TandemFunction
                    {
                        FunctionType = function.FunctionType,
                        TableNumber = function.TableNumber,
                    })
                    .ToList();
            }

            await ReadDeviceTextAsync(
                TandemDeviceInfoType.ModelName,
                value => report.Handshake.Model = value,
                report,
                cancellationToken);
            await ReadDeviceTextAsync(
                TandemDeviceInfoType.FirmwareVersion,
                value => report.Handshake.Firmware = value,
                report,
                cancellationToken);

            if (request.QueryFeatures && report.Handshake.ProtocolVersion is not null)
            {
                await QueryFeaturesAsync(report, cancellationToken);
            }

            if (request.QuerySnapshot && report.Handshake.ProtocolVersion is not null)
            {
                await QuerySnapshotAsync(report, cancellationToken);
            }

            if (request.ApplyEq && report.Handshake.ProtocolVersion is not null)
            {
                await ApplyEqAsync(request, report, cancellationToken);
            }

            if (request.ApplyNcAsm && report.Handshake.ProtocolVersion is not null)
            {
                await ApplyNcAsmAsync(request, report, cancellationToken);
            }

            if (request.ApplyMultipoint && report.Handshake.ProtocolVersion is not null)
            {
                await ApplyMultipointAsync(request, report, cancellationToken);
            }

            if (request.ApplyAutoPowerOff && report.Handshake.ProtocolVersion is not null)
            {
                await ApplyAutoPowerOffAsync(request, report, cancellationToken);
            }

            if (request.ApplyListeningMode && report.Handshake.ProtocolVersion is not null)
            {
                await ApplyListeningModeAsync(request, report, cancellationToken);
            }

            report.Status = report.Handshake.ProtocolVersion is not null
                ? "ok"
                : "handshake_incomplete";
            report.Message = report.Status == "ok"
                ? "Tandem handshake completed."
                : "Tandem service is present but the protocol handshake did not complete.";
        }
        catch (OperationCanceledException)
        {
            report.Status = "cancelled";
            report.Message = "Tandem session was cancelled.";
        }
        catch (Exception ex)
        {
            report.Status = "error";
            report.Message = $"{ex.GetType().Name}: {ex.Message}";
            report.Errors.Add(ex.ToString());
        }
        finally
        {
            await CleanupAsync(report);
            report.CompletedAt = DateTimeOffset.Now;
        }

        return report;
    }

    private static async Task<BluetoothLEDevice?> OpenDeviceAsync(
        ProbeRequest request,
        TandemSessionReport report,
        CancellationToken cancellationToken)
    {
        var addressType = request.AddressType switch
        {
            "public" => BluetoothAddressType.Public,
            "random" => BluetoothAddressType.Random,
            _ => BluetoothAddressType.Unspecified,
        };

        if (BluetoothAddress.TryParse(request.Address, out var address))
        {
            var device = await BluetoothLEDevice.FromBluetoothAddressAsync(address, addressType);
            if (device is not null)
            {
                return device;
            }
        }

        var devices = await DeviceInformation.FindAllAsync(BluetoothLEDevice.GetDeviceSelector());
        var target = devices.FirstOrDefault(device =>
            (!string.IsNullOrWhiteSpace(request.Name)
                && (device.Name ?? string.Empty).Contains(request.Name, StringComparison.OrdinalIgnoreCase))
            || AddressMatches(device, request.Address));

        if (target is not null)
        {
            var device = await BluetoothLEDevice.FromIdAsync(target.Id);
            if (device is not null)
            {
                return device;
            }
        }

        var scan = await new BleAdvertisementScanner().ScanAsync(TimeSpan.FromSeconds(5), cancellationToken);
        var advertisement = scan.Advertisements.FirstOrDefault(item =>
            !string.IsNullOrWhiteSpace(request.Name)
            && (item.LocalName ?? string.Empty).Contains(request.Name, StringComparison.OrdinalIgnoreCase));

        if (advertisement is not null && BluetoothAddress.TryParse(advertisement.Address, out var advertisedAddress))
        {
            return await BluetoothLEDevice.FromBluetoothAddressAsync(advertisedAddress, BluetoothAddressType.Random);
        }

        return null;
    }

    private static bool AddressMatches(DeviceInformation device, string? requestedAddress)
    {
        if (string.IsNullOrWhiteSpace(requestedAddress))
        {
            return false;
        }

        var address = device.Properties.TryGetValue("System.Devices.Aep.DeviceAddress", out var value)
            ? value?.ToString()
            : device.Id;
        return BluetoothAddress.Normalize(address) == BluetoothAddress.Normalize(requestedAddress);
    }

    private async Task TryBootstrapTandemAsync(
        IReadOnlyList<GattDeviceService> services,
        TandemSessionReport report,
        CancellationToken cancellationToken)
    {
        report.PairingBootstrapAttempted = true;

        var pairing = services.FirstOrDefault(service => service.Uuid == PairingCompleteNameServiceUuid);
        if (pairing is null)
        {
            report.PairingBootstrapResult = "pairing-name service missing";
            return;
        }

        GattCharacteristicsResult characteristics;
        try
        {
            characteristics = await pairing.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
        }
        catch (Exception ex)
        {
            report.PairingBootstrapResult = $"{ex.GetType().Name}: {ex.Message}";
            return;
        }

        if (characteristics.Status != GattCommunicationStatus.Success)
        {
            report.PairingBootstrapResult = $"characteristic discovery failed: {characteristics.Status}";
            return;
        }

        var mode = Find(characteristics, BluetoothModeUuid);
        var status = Find(characteristics, BluetoothModeStatusUuid);
        var completeName = Find(characteristics, CompleteNameUuid);
        var syncIndex = Find(characteristics, SyncNameIndexUuid);
        if (mode is null)
        {
            report.PairingBootstrapResult = "BLUETOOTH_MODE missing";
            return;
        }

        var modeStatusNotification = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var nameNotification = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        TypedEventHandler<GattCharacteristic, GattValueChangedEventArgs> modeHandler = (_, args) =>
        {
            modeStatusNotification.TrySetResult(ReadBuffer(args.CharacteristicValue));
        };
        TypedEventHandler<GattCharacteristic, GattValueChangedEventArgs> nameHandler = (_, args) =>
        {
            nameNotification.TrySetResult(ReadBuffer(args.CharacteristicValue));
        };

        if (status is not null)
        {
            status.ValueChanged += modeHandler;
            await status.WriteClientCharacteristicConfigurationDescriptorAsync(
                GattClientCharacteristicConfigurationDescriptorValue.Notify);
        }

        if (completeName is not null)
        {
            completeName.ValueChanged += nameHandler;
            await completeName.WriteClientCharacteristicConfigurationDescriptorAsync(
                GattClientCharacteristicConfigurationDescriptorValue.Notify);
        }

        try
        {
            var writeStatus = await WriteAsync(mode, [0x01]);
            report.PairingBootstrapResult = $"write={writeStatus}";

            if (status is not null)
            {
                var completed = await Task.WhenAny(
                    modeStatusNotification.Task,
                    Task.Delay(3_000, cancellationToken));
                report.PairingBootstrapResult += completed == modeStatusNotification.Task
                    ? $", modeNotify={Convert.ToHexString(await modeStatusNotification.Task).ToLowerInvariant()}"
                    : ", modeNotify=timeout";
            }

            if (completeName is not null && syncIndex is not null)
            {
                var nameBytes = new List<byte>();
                for (var chunk = 0; chunk < 20; chunk++)
                {
                    var completed = await Task.WhenAny(
                        nameNotification.Task,
                        Task.Delay(3_000, cancellationToken));
                    if (completed != nameNotification.Task)
                    {
                        report.PairingBootstrapResult += ", name=timeout";
                        break;
                    }

                    var payload = await nameNotification.Task;
                    nameNotification = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
                    completeName.ValueChanged -= nameHandler;
                    nameHandler = (_, args) => nameNotification.TrySetResult(ReadBuffer(args.CharacteristicValue));
                    completeName.ValueChanged += nameHandler;

                    if (payload.Length < 2)
                    {
                        report.PairingBootstrapResult += ", name=invalid";
                        break;
                    }

                    var totalLength = payload[0];
                    var chunkLength = payload[1];
                    if (chunkLength > payload.Length - 2)
                    {
                        report.PairingBootstrapResult += ", name=invalid-chunk";
                        break;
                    }

                    nameBytes.AddRange(payload.Skip(2).Take(chunkLength));
                    if (nameBytes.Count >= totalLength)
                    {
                        report.PairingBootstrapResult +=
                            $", name={Convert.ToHexString(nameBytes.ToArray()).ToLowerInvariant()}";
                        break;
                    }

                    await WriteAsync(syncIndex, [(byte)nameBytes.Count]);
                }
            }

            await Task.Delay(5_000, cancellationToken);
        }
        finally
        {
            if (status is not null)
            {
                status.ValueChanged -= modeHandler;
                try
                {
                    await status.WriteClientCharacteristicConfigurationDescriptorAsync(
                        GattClientCharacteristicConfigurationDescriptorValue.None);
                }
                catch
                {
                    // Best effort.
                }
            }

            if (completeName is not null)
            {
                completeName.ValueChanged -= nameHandler;
                try
                {
                    await completeName.WriteClientCharacteristicConfigurationDescriptorAsync(
                        GattClientCharacteristicConfigurationDescriptorValue.None);
                }
                catch
                {
                    // Best effort.
                }
            }
        }
    }
    private static GattCharacteristic? Find(GattCharacteristicsResult result, Guid uuid) =>
        result.Characteristics.FirstOrDefault(characteristic => characteristic.Uuid == uuid);

    private async Task ObserveMtuAsync(
        GattCharacteristic characteristic,
        TandemSessionReport report,
        CancellationToken cancellationToken)
    {
        _mtuPending = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        TypedEventHandler<GattCharacteristic, GattValueChangedEventArgs> handler = (_, args) =>
        {
            _mtuPending?.TrySetResult(ReadBuffer(args.CharacteristicValue));
        };

        characteristic.ValueChanged += handler;
        try
        {
            var status = await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                GattClientCharacteristicConfigurationDescriptorValue.Notify);
            if (status != GattCommunicationStatus.Success)
            {
                report.Warnings.Add($"DETERMINE_MTU notify failed: {status}.");
                return;
            }

            var completed = await Task.WhenAny(
                _mtuPending.Task,
                Task.Delay(2_000, cancellationToken));
            if (completed == _mtuPending.Task)
            {
                var value = await _mtuPending.Task;
                if (value.Length > 0)
                {
                    report.Handshake.DeterminedMtu = value[0];
                }
            }
            else
            {
                report.Warnings.Add("DETERMINE_MTU notification timed out.");
            }
        }
        finally
        {
            characteristic.ValueChanged -= handler;
            try
            {
                await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                    GattClientCharacteristicConfigurationDescriptorValue.None);
            }
            catch
            {
                // Best effort.
            }

            _mtuPending = null;
        }
    }

    private async Task<byte[]?> ReadAsync(
        GattCharacteristic characteristic,
        CancellationToken cancellationToken)
    {
        var result = await characteristic.ReadValueAsync(BluetoothCacheMode.Uncached);
        cancellationToken.ThrowIfCancellationRequested();
        return result.Status == GattCommunicationStatus.Success && result.Value is not null
            ? ReadBuffer(result.Value)
            : null;
    }

    private async Task<TandemNcAsmState?> ReadNcAsmStateAsync(
        TandemSessionReport report,
        CancellationToken cancellationToken)
    {
        var response = await SendAndWaitAsync(
            TandemV2Table1Messages.NcAsmGetParam(TandemNcAsmInquiredType.ModeNcAsmDualNcModeSwitchAndAsmSeamlessNa),
            [
                (byte)TandemV2Table1Command.NcAsmRetParam,
                (byte)TandemV2Table1Command.NcAsmNtfyParam,
            ],
            report,
            cancellationToken);
        return response is not null
            && TandemV2Table1Messages.TryParseNcAsmState(response.Payload, out var state)
                ? state
                : null;
    }

    private async Task ApplyNcAsmAsync(
        ProbeRequest request,
        TandemSessionReport report,
        CancellationToken cancellationToken)
    {
        var change = new TandemNcAsmChange();
        report.NcAsm = change;

        change.Before = await ReadNcAsmStateAsync(report, cancellationToken);

        var requestedMode = request.NcAsmMode
            ?? (change.Before?.Mode == TandemNcNcssAsmMode.Asm
                ? TandemNcAsmMode.Asm
                : TandemNcAsmMode.Nc);
        var mode = requestedMode == TandemNcAsmMode.Nc
            ? TandemNcNcssAsmMode.Nc
            : TandemNcNcssAsmMode.Asm;
        var ambient = request.NcAsmVoiceFocus
            ? TandemAmbientSoundMode.Voice
            : mode == TandemNcNcssAsmMode.Asm
                ? TandemAmbientSoundMode.Normal
                : change.Before?.AmbientMode ?? TandemAmbientSoundMode.Normal;
        var level = request.NcAsmLevel;
        var sensitivity = request.NcAsmSensitivity;
        change.Requested = new TandemNcAsmState(
            mode,
            ambient,
            level,
            request.NcAsmEnabled ? TandemNcAsmOnOffValue.On : TandemNcAsmOnOffValue.Off,
            request.NcAsmNoiseAdaptive ? TandemNcAsmOnOffValue.On : TandemNcAsmOnOffValue.Off,
            sensitivity,
            TandemValueChangeStatus.Changed);

        var setResponse = await SendAndWaitAsync(
            TandemV2Table1Messages.NcAsmSetMode(
                mode,
                ambient,
                level,
                sensitivity,
                request.NcAsmEnabled,
                request.NcAsmNoiseAdaptive),
            [
                (byte)TandemV2Table1Command.NcAsmRetParam,
                (byte)TandemV2Table1Command.NcAsmNtfyParam,
            ],
            report,
            cancellationToken);
        change.ResponseHex = setResponse is null
            ? null
            : Convert.ToHexString(setResponse.Payload).ToLowerInvariant();

        var after = await ReadNcAsmStateAsync(report, cancellationToken);
        if (after is null
            && setResponse is not null
            && TandemV2Table1Messages.TryParseNcAsmState(setResponse.Payload, out var notified))
        {
            after = notified;
        }

        change.After = after;
        change.Verified = after is not null
            && after.OnOff == change.Requested.OnOff
            && (after.OnOff == TandemNcAsmOnOffValue.Off
                || (after.Mode == mode
                    && (mode == TandemNcNcssAsmMode.Nc
                        || (after.AmbientMode == ambient
                            && after.Level == level
                            && after.NoiseAdaptiveOnOff == change.Requested.NoiseAdaptiveOnOff
                            && after.Sensitivity == sensitivity))));
        change.Status = change.Verified
            ? "verified"
            : "write_sent_unverified";
    }

    private async Task<TandemEqState?> ReadEqStateAsync(
        TandemSessionReport report,
        CancellationToken cancellationToken)
    {
        var response = await SendAndWaitAsync(
            TandemV2Table1Messages.EqEbbGetParam(TandemEqEbbInquiredType.PresetEqAndErrorCode),
            [(byte)TandemV2Table1Command.EqEbbRetParam],
            report,
            cancellationToken);
        if (response is null
            || !TandemV2Table1Messages.TryParseEqParam(
                response.Payload,
                out var preset,
                out var bands))
        {
            return null;
        }

        return new TandemEqState
        {
            Preset = preset,
            Bands = bands,
            RawHex = Convert.ToHexString(response.Payload).ToLowerInvariant(),
        };
    }

    private async Task ApplyEqAsync(
        ProbeRequest request,
        TandemSessionReport report,
        CancellationToken cancellationToken)
    {
        var change = new TandemEqChange();
        report.Eq = change;

        var before = await ReadEqStateAsync(report, cancellationToken);
        if (before is null)
        {
            change.Status = "read_failed";
            report.Warnings.Add("EQ state read failed; no EQ write was sent.");
            return;
        }

        change.Before = before;
        var preset = request.EqPreset ?? before.Preset;
        var requestedBands = request.EqBands?.ToArray();
        var usePresetDefault = preset != TandemEqPresetId.Custom && requestedBands is null;
        var bands = requestedBands ?? before.Bands;
        if (bands.Length > 255)
        {
            change.Status = "invalid_bands";
            report.Warnings.Add("EQ band count exceeds 255; no EQ write was sent.");
            return;
        }

        change.UsedPresetDefault = usePresetDefault;
        change.Requested = new TandemEqState
        {
            Preset = preset,
            Bands = usePresetDefault ? [] : bands,
        };

        var setResponse = await SendAndWaitAsync(
            usePresetDefault
                ? TandemV2Table1Messages.EqEbbSetPresetOnly(
                    TandemEqEbbInquiredType.PresetEqAndErrorCode,
                    preset)
                : TandemV2Table1Messages.EqEbbSetPreset(
                    TandemEqEbbInquiredType.PresetEqAndErrorCode,
                    preset,
                    bands),
            [(byte)TandemV2Table1Command.EqEbbRetParam],
            report,
            cancellationToken);
        change.ResponseHex = setResponse is null
            ? null
            : Convert.ToHexString(setResponse.Payload).ToLowerInvariant();

        var after = await ReadEqStateAsync(report, cancellationToken);
        change.After = after;
        change.Verified = after is not null
            && after.Preset == preset
            && (usePresetDefault || after.Bands.SequenceEqual(bands));
        change.Status = change.Verified
            ? "verified"
            : setResponse is null
                ? "write_unconfirmed"
                : "mismatch";
    }

    private async Task<TandemGsCapability?> ReadGsCapabilityAsync(
        TandemGsInquiredType type,
        TandemSessionReport report,
        CancellationToken cancellationToken,
        bool recordTimeoutWarning = true)
    {
        var response = await SendAndWaitAsync(
            TandemV2Table1Messages.GeneralSettingGetCapability(type),
            [(byte)TandemV2Table1Command.GeneralSettingRetCapability],
            report,
            cancellationToken,
            recordTimeoutWarning);
        return response is not null
            && TandemV2Table1Messages.TryParseGsCapability(response.Payload, out var capability)
                ? capability
                : null;
    }

    private async Task<TandemGsInquiredType?> FindMultipointGsTypeAsync(
        TandemSessionReport report,
        CancellationToken cancellationToken)
    {
        if (_multipointGsType is { } cached)
        {
            return cached;
        }

        foreach (var type in new[]
        {
            TandemGsInquiredType.GeneralSetting1,
            TandemGsInquiredType.GeneralSetting2,
            TandemGsInquiredType.GeneralSetting3,
            TandemGsInquiredType.GeneralSetting4,
        })
        {
            var capability = await ReadGsCapabilityAsync(
                type,
                report,
                cancellationToken,
                recordTimeoutWarning: false);
            if (capability is null)
            {
                continue;
            }

            if (capability.Title != "MULTIPOINT_SETTING")
            {
                continue;
            }

            if (capability.SettingType != TandemGsSettingType.BooleanType)
            {
                report.Warnings.Add(
                    $"Multipoint setting uses unsupported type {capability.SettingType}.");
                return null;
            }

            _multipointGsType = type;
            return type;
        }

        report.Warnings.Add("The device did not report a MULTIPOINT_SETTING capability.");
        return null;
    }

    private async Task<bool?> ReadMultipointAsync(
        TandemSessionReport report,
        CancellationToken cancellationToken)
    {
        var type = await FindMultipointGsTypeAsync(report, cancellationToken);
        if (type is null)
        {
            return null;
        }

        var response = await SendAndWaitAsync(
            TandemV2Table1Messages.GeneralSettingGetParam(type.Value),
            [(byte)TandemV2Table1Command.GeneralSettingRetParam],
            report,
            cancellationToken);
        if (response is null
            || !TandemV2Table1Messages.TryParseGsState(response.Payload, out var state)
            || state.SettingType != TandemGsSettingType.BooleanType)
        {
            return null;
        }

        return state.Value == TandemGsSettingValue.On;
    }

    private async Task ApplyMultipointAsync(
        ProbeRequest request,
        TandemSessionReport report,
        CancellationToken cancellationToken)
    {
        var change = new TandemMultipointChange();
        report.Multipoint = change;

        var type = await FindMultipointGsTypeAsync(report, cancellationToken);
        if (type is null)
        {
            change.Status = "unsupported";
            return;
        }

        change.Before = await ReadMultipointAsync(report, cancellationToken);
        var enabled = request.MultipointEnabled ?? !(change.Before ?? false);
        change.Requested = enabled;

        var response = await SendAndWaitAsync(
            TandemV2Table1Messages.GeneralSettingSetBool(type.Value, enabled),
            [
                (byte)TandemV2Table1Command.GeneralSettingRetParam,
                (byte)TandemV2Table1Command.GeneralSettingNtnyParam,
            ],
            report,
            cancellationToken);
        change.ResponseHex = response is null
            ? null
            : Convert.ToHexString(response.Payload).ToLowerInvariant();

        // Disabling multipoint can take a moment while the secondary link is dropped.
        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        change.After = await ReadMultipointAsync(report, cancellationToken);
        change.Verified = change.After == enabled;
        change.Status = change.Verified
            ? "verified"
            : "write_sent_unverified";
    }

    private async Task<TandemAutoPowerOffCapability?> ReadAutoPowerOffCapabilityAsync(
        TandemSessionReport report,
        CancellationToken cancellationToken)
    {
        if (_autoPowerOffCapability is { } cached)
        {
            return cached;
        }

        var response = await SendAndWaitAsync(
            TandemV2Table1Messages.PowerGetCapability(
                TandemPowerInquiredType.AutoPowerOffWearingDetection),
            [(byte)TandemV2Table1Command.PowerRetCapability],
            report,
            cancellationToken);
        if (response is null
            || !TandemV2Table1Messages.TryParseAutoPowerOffCapability(
                response.Payload,
                out var capability))
        {
            return null;
        }

        _autoPowerOffCapability = capability;
        return capability;
    }

    private async Task<TandemAutoPowerOffState?> ReadAutoPowerOffStateAsync(
        TandemSessionReport report,
        CancellationToken cancellationToken)
    {
        var response = await SendAndWaitAsync(
            TandemV2Table1Messages.PowerGetParam(
                TandemPowerInquiredType.AutoPowerOffWearingDetection),
            [(byte)TandemV2Table1Command.PowerRetParam],
            report,
            cancellationToken);
        return response is not null
            && TandemV2Table1Messages.TryParseAutoPowerOffState(response.Payload, out var state)
                ? state
                : null;
    }

    private async Task<TandemAutoPowerOffSnapshot> ReadAutoPowerOffSnapshotAsync(
        TandemSessionReport report,
        CancellationToken cancellationToken)
    {
        await ReadAutoPowerOffCapabilityAsync(report, cancellationToken);
        return new TandemAutoPowerOffSnapshot
        {
            State = await ReadAutoPowerOffStateAsync(report, cancellationToken),
            Elements =
            [
                TandemAutoPowerOffElement.WhenRemovedFromEars,
                TandemAutoPowerOffElement.Disable,
            ],
        };
    }

    private async Task ApplyAutoPowerOffAsync(
        ProbeRequest request,
        TandemSessionReport report,
        CancellationToken cancellationToken)
    {
        var change = new TandemAutoPowerOffChange();
        report.AutoPowerOff = change;

        var snapshot = await ReadAutoPowerOffSnapshotAsync(report, cancellationToken);
        change.Elements = snapshot.Elements;

        change.Before = snapshot.State;
        var active = request.AutoPowerOffElement
            ?? snapshot.State?.First
            ?? TandemAutoPowerOffElement.Disable;
        if (active is not (TandemAutoPowerOffElement.WhenRemovedFromEars
            or TandemAutoPowerOffElement.Disable))
        {
            change.Status = "unsupported_value";
            report.Warnings.Add(
                "Auto power off supports only WhenRemovedFromEars or Disable.");
            return;
        }

        var fillerTime = snapshot.State?.Second ?? TandemAutoPowerOffElement.In5Minutes;
        var requested = new TandemAutoPowerOffState(active, fillerTime);
        change.Requested = requested;

        var response = await SendAndWaitAsync(
            TandemV2Table1Messages.PowerSetAutoPowerOff(
                requested.First,
                requested.Second,
                TandemPowerInquiredType.AutoPowerOffWearingDetection),
            [
                (byte)TandemV2Table1Command.PowerRetParam,
                (byte)TandemV2Table1Command.PowerNtfyParam,
            ],
            report,
            cancellationToken);
        change.ResponseHex = response is null
            ? null
            : Convert.ToHexString(response.Payload).ToLowerInvariant();

        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        change.After = await ReadAutoPowerOffStateAsync(report, cancellationToken);
        change.Verified = change.After?.First == requested.First;
        change.Status = change.Verified
            ? "verified"
            : "write_sent_unverified";
    }

    private async Task<TandemAudioInquiredType?> ResolveBgmAudioTypeAsync(
        TandemSessionReport report,
        CancellationToken cancellationToken)
    {
        if (_bgmAudioType is { } cached)
        {
            return cached;
        }

        foreach (var type in new[]
        {
            TandemAudioInquiredType.BgmModeAndErrorCode,
            TandemAudioInquiredType.BgmMode,
        })
        {
            var response = await SendAndWaitAsync(
                TandemV2Table1Messages.AudioGetCapability(type),
                [(byte)TandemV2Table1Command.AudioRetCapability],
                report,
                cancellationToken,
                recordTimeoutWarning: false);
            if (IsAudioCapabilityFor(response, type))
            {
                _bgmAudioType = type;
                return type;
            }
        }

        report.Warnings.Add("The device did not report a BGM audio capability.");
        return null;
    }

    private async Task<CinemaAudioPath> ResolveCinemaAudioPathAsync(
        TandemSessionReport report,
        CancellationToken cancellationToken)
    {
        if (_cinemaAudioPath is { } cached)
        {
            return cached;
        }

        var paths = new (CinemaAudioPath Path, TandemAudioInquiredType Type)[]
        {
            (CinemaAudioPath.UpmixCinema, TandemAudioInquiredType.UpmixCinema),
            (CinemaAudioPath.UpmixSeries, TandemAudioInquiredType.UpmixSeries),
        };

        foreach (var candidate in paths)
        {
            var response = await SendAndWaitAsync(
                TandemV2Table1Messages.AudioGetCapability(candidate.Type),
                [(byte)TandemV2Table1Command.AudioRetCapability],
                report,
                cancellationToken,
                recordTimeoutWarning: false);
            if (IsAudioCapabilityFor(response, candidate.Type))
            {
                _cinemaAudioPath = candidate.Path;
                return candidate.Path;
            }
        }

        _cinemaAudioPath = CinemaAudioPath.None;
        return CinemaAudioPath.None;
    }

    private static bool IsAudioCapabilityFor(
        TandemFrame? frame,
        TandemAudioInquiredType type) =>
        frame is { Payload.Length: >= 2 }
        && frame.Payload[0] == (byte)TandemV2Table1Command.AudioRetCapability
        && frame.Payload[1] == (byte)type;

    private async Task<TandemBgmState?> ReadBgmStateAsync(
        TandemSessionReport report,
        CancellationToken cancellationToken)
    {
        var type = await ResolveBgmAudioTypeAsync(report, cancellationToken);
        if (type is null)
        {
            return null;
        }

        var response = await SendAndWaitAsync(
            TandemV2Table1Messages.AudioGetParam(type.Value),
            [(byte)TandemV2Table1Command.AudioRetParam],
            report,
            cancellationToken);
        return response is not null
            && TandemV2Table1Messages.TryParseBgmState(response.Payload, out var state)
                ? state
                : null;
    }

    private async Task<TandemCinemaState?> ReadCinemaStateAsync(
        TandemSessionReport report,
        CancellationToken cancellationToken)
    {
        var path = await ResolveCinemaAudioPathAsync(report, cancellationToken);
        if (path == CinemaAudioPath.None)
        {
            return null;
        }

        var type = path == CinemaAudioPath.UpmixSeries
            ? TandemAudioInquiredType.UpmixSeries
            : TandemAudioInquiredType.UpmixCinema;
        var response = await SendAndWaitAsync(
            TandemV2Table1Messages.AudioGetParam(type),
            [(byte)TandemV2Table1Command.AudioRetParam],
            report,
            cancellationToken);
        if (response is null)
        {
            return null;
        }

        if (path == CinemaAudioPath.UpmixSeries)
        {
            return TandemV2Table1Messages.TryParseUpmixSeriesState(
                response.Payload,
                out var item)
                    ? new TandemCinemaState(item == TandemUpmixItemId.Cinema
                        ? TandemOnOffSettingValue.On
                        : TandemOnOffSettingValue.Off)
                    : null;
        }

        return TandemV2Table1Messages.TryParseCinemaState(response.Payload, out var state)
            ? state
            : null;
    }

    private async Task<TandemListeningModeState?> ReadListeningModeAsync(
        TandemSessionReport report,
        CancellationToken cancellationToken)
    {
        var bgm = await ReadBgmStateAsync(report, cancellationToken);
        var cinema = await ReadCinemaStateAsync(report, cancellationToken);
        if (bgm is null && cinema is null)
        {
            return null;
        }

        var bgmOn = bgm?.OnOff == TandemOnOffSettingValue.On;
        var cinemaOn = cinema?.OnOff == TandemOnOffSettingValue.On;
        var mode = bgmOn
            ? TandemListeningMode.BackgroundMusic
            : cinemaOn
                ? TandemListeningMode.Cinema
                : TandemListeningMode.Standard;
        return new TandemListeningModeState(
            mode,
            bgm?.OnOff ?? TandemOnOffSettingValue.OutOfRange,
            bgm?.RoomSize ?? TandemRoomSize.Middle,
            cinema?.OnOff ?? TandemOnOffSettingValue.OutOfRange);
    }

    private async Task ApplyListeningModeAsync(
        ProbeRequest request,
        TandemSessionReport report,
        CancellationToken cancellationToken)
    {
        var change = new TandemListeningModeChange();
        report.ListeningMode = change;

        var before = await ReadListeningModeAsync(report, cancellationToken);
        if (before is null)
        {
            change.Status = "read_failed";
            report.Warnings.Add("Listening mode state read failed; no write was sent.");
            return;
        }

        change.Before = before;
        var mode = request.ListeningMode ?? TandemListeningMode.Standard;
        var roomSize = request.BgmRoomSize ?? before.RoomSize;
        var bgmOn = mode == TandemListeningMode.BackgroundMusic;
        var cinemaOn = mode == TandemListeningMode.Cinema;
        var requested = new TandemListeningModeState(
            mode,
            bgmOn ? TandemOnOffSettingValue.On : TandemOnOffSettingValue.Off,
            roomSize,
            cinemaOn ? TandemOnOffSettingValue.On : TandemOnOffSettingValue.Off);
        change.Requested = requested;

        var bgmType = await ResolveBgmAudioTypeAsync(report, cancellationToken);
        if (mode == TandemListeningMode.BackgroundMusic && bgmType is null)
        {
            change.Status = "unsupported";
            return;
        }

        var cinemaPath = await ResolveCinemaAudioPathAsync(report, cancellationToken);
        if (mode == TandemListeningMode.Cinema && cinemaPath == CinemaAudioPath.None)
        {
            change.Status = "unsupported";
            return;
        }

        TandemFrame? lastResponse = null;
        if (bgmType is not null)
        {
            lastResponse = await SendAndWaitAsync(
                TandemV2Table1Messages.AudioSetBgm(bgmOn, roomSize, bgmType.Value),
                [
                    (byte)TandemV2Table1Command.AudioRetParam,
                    (byte)TandemV2Table1Command.AudioNtfyParam,
                ],
                report,
                cancellationToken) ?? lastResponse;
        }

        if (cinemaPath != CinemaAudioPath.None)
        {
            var cinemaCommand = cinemaPath == CinemaAudioPath.UpmixSeries
                ? TandemV2Table1Messages.AudioSetUpmixSeries(cinemaOn
                    ? TandemUpmixItemId.Cinema
                    : TandemUpmixItemId.None)
                : TandemV2Table1Messages.AudioSetCinema(cinemaOn);
            lastResponse = await SendAndWaitAsync(
                cinemaCommand,
                [
                    (byte)TandemV2Table1Command.AudioRetParam,
                    (byte)TandemV2Table1Command.AudioNtfyParam,
                ],
                report,
                cancellationToken) ?? lastResponse;
        }

        change.ResponseHex = lastResponse is null
            ? null
            : Convert.ToHexString(lastResponse.Payload).ToLowerInvariant();

        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        change.After = await ReadListeningModeAsync(report, cancellationToken);
        change.Verified = change.After is not null
            && change.After.Mode == mode
            && (mode != TandemListeningMode.BackgroundMusic
                || change.After.RoomSize == roomSize);
        change.Status = change.Verified
            ? "verified"
            : "write_sent_unverified";
    }

    private async Task QuerySnapshotAsync(
        TandemSessionReport report,
        CancellationToken cancellationToken)
    {
        var snapshot = new TandemDeviceSnapshot();
        report.Snapshot = snapshot;

        var ncAsm = await SendAndWaitAsync(
            TandemV2Table1Messages.NcAsmGetParam(TandemNcAsmInquiredType.ModeNcAsmDualNcModeSwitchAndAsmSeamlessNa),
            [
                (byte)TandemV2Table1Command.NcAsmRetParam,
                (byte)TandemV2Table1Command.NcAsmNtfyParam,
            ],
            report,
            cancellationToken);
        if (ncAsm is not null
            && TandemV2Table1Messages.TryParseNcAsmState(ncAsm.Payload, out var state))
        {
            snapshot.NcAsm = state;
        }

        var eq = await SendAndWaitAsync(
            TandemV2Table1Messages.EqEbbGetParam(TandemEqEbbInquiredType.PresetEqAndErrorCode),
            [(byte)TandemV2Table1Command.EqEbbRetParam],
            report,
            cancellationToken);
        if (eq is not null
            && TandemV2Table1Messages.TryParseEqParam(eq.Payload, out var preset, out var bands))
        {
            snapshot.Eq = new TandemEqState
            {
                Preset = preset,
                Bands = bands,
                RawHex = Convert.ToHexString(eq.Payload).ToLowerInvariant(),
            };
        }

        var battery = await SendAndWaitAsync(
            TandemV2Table1Messages.PowerGetStatus(TandemPowerInquiredType.Battery),
            [(byte)TandemV2Table1Command.PowerRetStatus],
            report,
            cancellationToken);
        if (battery is { Payload.Length: >= 3 }
            && battery.Payload[0] == (byte)TandemV2Table1Command.PowerRetStatus)
        {
            snapshot.BatteryPercent = battery.Payload[2];
            snapshot.BatteryStatus = battery.Payload.Length > 3 ? battery.Payload[3] : null;
        }

        snapshot.MultipointEnabled = await ReadMultipointAsync(report, cancellationToken);

        var autoPowerOff = await ReadAutoPowerOffSnapshotAsync(report, cancellationToken);
        snapshot.AutoPowerOffElements = autoPowerOff.Elements;
        snapshot.AutoPowerOff = autoPowerOff.State;

        snapshot.ListeningMode = await ReadListeningModeAsync(report, cancellationToken);
    }

    private async Task QueryFeaturesAsync(
        TandemSessionReport report,
        CancellationToken cancellationToken)
    {
        var queries = new (string Name, byte[] Payload, byte Expected)[]
        {
            ("ncas m capability type 19", TandemV2Table1Messages.NcAsmGetCapability(TandemNcAsmInquiredType.NcOnOffAndAsmSeamless), (byte)TandemV2Table1Command.NcAsmRetCapability),
            ("ncas m status type 19", TandemV2Table1Messages.NcAsmGetStatus(TandemNcAsmInquiredType.NcOnOffAndAsmSeamless), (byte)TandemV2Table1Command.NcAsmRetStatus),
            ("ncas m capability type 21", TandemV2Table1Messages.NcAsmGetCapability(TandemNcAsmInquiredType.ModeNcAsmAutoNcModeSwitchAndAsmSeamless), (byte)TandemV2Table1Command.NcAsmRetCapability),
            ("ncas m status type 21", TandemV2Table1Messages.NcAsmGetStatus(TandemNcAsmInquiredType.ModeNcAsmAutoNcModeSwitchAndAsmSeamless), (byte)TandemV2Table1Command.NcAsmRetStatus),
            ("eq ebb capability preset", TandemV2Table1Messages.EqEbbGetCapability(TandemEqEbbInquiredType.PresetEq), (byte)TandemV2Table1Command.EqEbbRetCapability),
            ("eq ebb status preset", TandemV2Table1Messages.EqEbbGetStatus(TandemEqEbbInquiredType.PresetEq), (byte)TandemV2Table1Command.EqEbbRetStatus),
            ("eq ebb capability preset+ult", TandemV2Table1Messages.EqEbbGetCapability(TandemEqEbbInquiredType.PresetEqAndUltMode), (byte)TandemV2Table1Command.EqEbbRetCapability),
            ("eq ebb status preset+ult", TandemV2Table1Messages.EqEbbGetStatus(TandemEqEbbInquiredType.PresetEqAndUltMode), (byte)TandemV2Table1Command.EqEbbRetStatus),
            ("ncas m status type 1", TandemV2Table1Messages.NcAsmGetStatus(TandemNcAsmInquiredType.NcOnOff), (byte)TandemV2Table1Command.NcAsmRetStatus),
            ("ncas m status type 22", TandemV2Table1Messages.NcAsmGetStatus(TandemNcAsmInquiredType.ModeNcAsmDualSingleNcModeSwitchAndAsmSeamless), (byte)TandemV2Table1Command.NcAsmRetStatus),
            ("eq ebb status preset+error", TandemV2Table1Messages.EqEbbGetStatus(TandemEqEbbInquiredType.PresetEqAndErrorCode), (byte)TandemV2Table1Command.EqEbbRetStatus),
            ("eq ebb status ebb", TandemV2Table1Messages.EqEbbGetStatus(TandemEqEbbInquiredType.Ebb), (byte)TandemV2Table1Command.EqEbbRetStatus),
            ("power battery status", TandemV2Table1Messages.PowerGetStatus(TandemPowerInquiredType.Battery), (byte)TandemV2Table1Command.PowerRetStatus),
            ("ncas m param type 19", TandemV2Table1Messages.NcAsmGetParam(TandemNcAsmInquiredType.NcOnOffAndAsmSeamless), (byte)TandemV2Table1Command.NcAsmRetParam),
            ("ncas m param type 1", TandemV2Table1Messages.NcAsmGetParam(TandemNcAsmInquiredType.NcOnOff), (byte)TandemV2Table1Command.NcAsmRetParam),
            ("ncas m param type 25", TandemV2Table1Messages.NcAsmGetParam(TandemNcAsmInquiredType.ModeNcAsmDualNcModeSwitchAndAsmSeamlessNa), (byte)TandemV2Table1Command.NcAsmRetParam),
            ("eq ebb param preset+error", TandemV2Table1Messages.EqEbbGetParam(TandemEqEbbInquiredType.PresetEqAndErrorCode), (byte)TandemV2Table1Command.EqEbbRetParam),
            ("eq ebb param preset", TandemV2Table1Messages.EqEbbGetParam(TandemEqEbbInquiredType.PresetEq), (byte)TandemV2Table1Command.EqEbbRetParam),
            ("gs capability d1", TandemV2Table1Messages.GeneralSettingGetCapability(TandemGsInquiredType.GeneralSetting1), (byte)TandemV2Table1Command.GeneralSettingRetCapability),
            ("gs capability d2", TandemV2Table1Messages.GeneralSettingGetCapability(TandemGsInquiredType.GeneralSetting2), (byte)TandemV2Table1Command.GeneralSettingRetCapability),
            ("gs capability d3", TandemV2Table1Messages.GeneralSettingGetCapability(TandemGsInquiredType.GeneralSetting3), (byte)TandemV2Table1Command.GeneralSettingRetCapability),
            ("gs capability d4", TandemV2Table1Messages.GeneralSettingGetCapability(TandemGsInquiredType.GeneralSetting4), (byte)TandemV2Table1Command.GeneralSettingRetCapability),
            ("gs status d1", TandemV2Table1Messages.GeneralSettingGetStatus(TandemGsInquiredType.GeneralSetting1), (byte)TandemV2Table1Command.GeneralSettingRetStatus),
            ("gs status d2", TandemV2Table1Messages.GeneralSettingGetStatus(TandemGsInquiredType.GeneralSetting2), (byte)TandemV2Table1Command.GeneralSettingRetStatus),
            ("gs status d3", TandemV2Table1Messages.GeneralSettingGetStatus(TandemGsInquiredType.GeneralSetting3), (byte)TandemV2Table1Command.GeneralSettingRetStatus),
            ("gs status d4", TandemV2Table1Messages.GeneralSettingGetStatus(TandemGsInquiredType.GeneralSetting4), (byte)TandemV2Table1Command.GeneralSettingRetStatus),
            ("gs param d1", TandemV2Table1Messages.GeneralSettingGetParam(TandemGsInquiredType.GeneralSetting1), (byte)TandemV2Table1Command.GeneralSettingRetParam),
            ("gs param d2", TandemV2Table1Messages.GeneralSettingGetParam(TandemGsInquiredType.GeneralSetting2), (byte)TandemV2Table1Command.GeneralSettingRetParam),
            ("gs param d3", TandemV2Table1Messages.GeneralSettingGetParam(TandemGsInquiredType.GeneralSetting3), (byte)TandemV2Table1Command.GeneralSettingRetParam),
            ("gs param d4", TandemV2Table1Messages.GeneralSettingGetParam(TandemGsInquiredType.GeneralSetting4), (byte)TandemV2Table1Command.GeneralSettingRetParam),
            ("power auto power off capability", TandemV2Table1Messages.PowerGetCapability(TandemPowerInquiredType.AutoPowerOff), (byte)TandemV2Table1Command.PowerRetCapability),
            ("power auto power off param", TandemV2Table1Messages.PowerGetParam(TandemPowerInquiredType.AutoPowerOff), (byte)TandemV2Table1Command.PowerRetParam),
            ("power auto power off wearing capability", TandemV2Table1Messages.PowerGetCapability(TandemPowerInquiredType.AutoPowerOffWearingDetection), (byte)TandemV2Table1Command.PowerRetCapability),
            ("power auto power off wearing param", TandemV2Table1Messages.PowerGetParam(TandemPowerInquiredType.AutoPowerOffWearingDetection), (byte)TandemV2Table1Command.PowerRetParam),
            ("audio bgm capability type 9", TandemV2Table1Messages.AudioGetCapability(TandemAudioInquiredType.BgmModeAndErrorCode), (byte)TandemV2Table1Command.AudioRetCapability),
            ("audio bgm capability type 3", TandemV2Table1Messages.AudioGetCapability(TandemAudioInquiredType.BgmMode), (byte)TandemV2Table1Command.AudioRetCapability),
            ("audio bgm param type 9", TandemV2Table1Messages.AudioGetParam(TandemAudioInquiredType.BgmModeAndErrorCode), (byte)TandemV2Table1Command.AudioRetParam),
            ("audio bgm param type 3", TandemV2Table1Messages.AudioGetParam(TandemAudioInquiredType.BgmMode), (byte)TandemV2Table1Command.AudioRetParam),
            ("audio cinema capability type 4", TandemV2Table1Messages.AudioGetCapability(TandemAudioInquiredType.UpmixCinema), (byte)TandemV2Table1Command.AudioRetCapability),
            ("audio cinema param type 4", TandemV2Table1Messages.AudioGetParam(TandemAudioInquiredType.UpmixCinema), (byte)TandemV2Table1Command.AudioRetParam),
            ("audio upmix series capability type 10", TandemV2Table1Messages.AudioGetCapability(TandemAudioInquiredType.UpmixSeries), (byte)TandemV2Table1Command.AudioRetCapability),
            ("audio upmix series param type 10", TandemV2Table1Messages.AudioGetParam(TandemAudioInquiredType.UpmixSeries), (byte)TandemV2Table1Command.AudioRetParam),
        };

        foreach (var query in queries)
        {
            var response = await SendAndWaitAsync(
                query.Payload,
                [query.Expected],
                report,
                cancellationToken);
            report.FeatureQueries.Add(new TandemFeatureQuery
            {
                Name = query.Name,
                CommandHex = $"0x{query.Payload[0]:x2}",
                InquiredType = query.Payload[1],
                Status = response is null ? "not_answered" : "ok",
                PayloadHex = response is null ? null : Convert.ToHexString(response.Payload).ToLowerInvariant(),
                Detail = response is null ? null : TandemV2Table1Messages.DescribeFeature(response.Payload),
            });
        }
    }

    private async Task ReadDeviceTextAsync(
        TandemDeviceInfoType type,
        Action<string> apply,
        TandemSessionReport report,
        CancellationToken cancellationToken)
    {
        var response = await SendAndWaitAsync(
            TandemV2Table1Messages.ConnectGetDeviceInfo(type),
            [(byte)TandemV2Table1Command.ConnectRetDeviceInfo],
            report,
            cancellationToken);
        if (response is not null
            && TandemV2Table1Messages.TryParseDeviceInfoText(response.Payload, out var parsedType, out var text)
            && parsedType == type)
        {
            apply(text);
        }
    }

    private async Task<TandemFrame?> SendAndWaitAsync(
        byte[] payload,
        IReadOnlyCollection<byte> expectedCommands,
        TandemSessionReport report,
        CancellationToken cancellationToken,
        bool recordTimeoutWarning = true)
    {
        if (_toAcc is null)
        {
            return null;
        }

        var sequence = _sequence;
        var raw = TandemFrameCodec.Encode(sequence, TandemDataType.DataMdr, payload);
        var completion = new TaskCompletionSource<TandemFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        var ackCompletion = new TaskCompletionSource<byte>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_pendingGate)
        {
            _pending = completion;
            _ackPending = ackCompletion;
            _pendingCommands = expectedCommands.ToHashSet();
        }

        var status = await WriteAsync(_toAcc, raw);
        report.Frames.Add(RecordFrame("tx", raw, sequence, TandemDataType.DataMdr, payload));
        if (status != GattCommunicationStatus.Success)
        {
            report.Warnings.Add($"Tandem write failed: {status}.");
            ClearPending();
            return null;
        }

        var ack = await Task.WhenAny(
            ackCompletion.Task,
            Task.Delay(TimeSpan.FromMilliseconds(1_500), cancellationToken));
        if (ack == ackCompletion.Task)
        {
            _sequence = await ackCompletion.Task;
        }
        else
        {
            report.Warnings.Add($"Tandem ACK timed out for command 0x{payload[0]:x2} (seq {sequence}).");
        }

        var completed = await Task.WhenAny(
            completion.Task,
            Task.Delay(TimeSpan.FromMilliseconds(5_000), cancellationToken));
        if (completed == completion.Task)
        {
            return await completion.Task;
        }

        if (recordTimeoutWarning)
        {
            report.Warnings.Add($"Tandem response timed out for command 0x{payload[0]:x2}.");
        }

        ClearPending();
        return null;
    }

    private async Task<GattCommunicationStatus> WriteAsync(GattCharacteristic characteristic, byte[] data)
    {
        var buffer = CreateBuffer(data);
        try
        {
            var result = await characteristic.WriteValueWithResultAsync(buffer, GattWriteOption.WriteWithoutResponse);
            return result.Status;
        }
        catch
        {
            return await characteristic.WriteValueAsync(buffer, GattWriteOption.WriteWithoutResponse);
        }
    }

    private void OnFromAccValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
    {
        var data = ReadBuffer(args.CharacteristicValue);
        var frames = new List<TandemFrame>();

        lock (_frameGate)
        {
            _receiveBuffer.AddRange(data);
            while (TryTakeFrame(out var frame))
            {
                frames.Add(frame);
            }
        }

        foreach (var frame in frames)
        {
            var record = RecordFrame(
                "rx",
                TandemFrameCodec.Encode(frame.Sequence, frame.DataType, frame.Payload),
                frame.Sequence,
                frame.DataType,
                frame.Payload);
            _report?.Frames.Add(record);

            if (frame.DataType == TandemDataType.Ack)
            {
                lock (_pendingGate)
                {
                    _ackPending?.TrySetResult(frame.Sequence);
                    _ackPending = null;
                }

                continue;
            }

            _ = SendAckAsync((byte)(1 - frame.Sequence));

            lock (_pendingGate)
            {
                if (_pending is not null && _pendingCommands?.Contains(frame.Command) == true)
                {
                    _pending.TrySetResult(frame);
                    _pending = null;
                    _pendingCommands = null;
                }
            }
        }
    }

    private async Task SendAckAsync(byte sequence)
    {
        if (_toAcc is null)
        {
            return;
        }

        try
        {
            await _toAcc.WriteValueAsync(
                CreateBuffer(TandemFrameCodec.EncodeAck(sequence)),
                GattWriteOption.WriteWithoutResponse);
        }
        catch
        {
            // ACK failure is non-fatal for diagnostics; the request waiter will time out.
        }
    }

    private bool TryTakeFrame(out TandemFrame frame)
    {
        frame = null!;
        while (_receiveBuffer.Count > 0)
        {
            var start = _receiveBuffer.IndexOf(TandemFrameCodec.Start);
            if (start < 0)
            {
                _receiveBuffer.Clear();
                return false;
            }

            if (start > 0)
            {
                _receiveBuffer.RemoveRange(0, start);
            }

            var end = _receiveBuffer.IndexOf(TandemFrameCodec.End, 1);
            if (end < 0)
            {
                return false;
            }

            var candidate = _receiveBuffer.Take(end + 1).ToArray();
            if (TandemFrameCodec.TryDecode(candidate, out var decoded, out _) && decoded is not null)
            {
                _receiveBuffer.RemoveRange(0, end + 1);
                frame = decoded;
                return true;
            }

            _receiveBuffer.RemoveRange(0, 1);
        }

        return false;
    }

    private void ClearPending()
    {
        lock (_pendingGate)
        {
            _pending = null;
            _ackPending = null;
            _pendingCommands = null;
        }
    }

    private static TandemFrameRecord RecordFrame(
        string direction,
        byte[] raw,
        byte sequence,
        TandemDataType dataType,
        byte[] payload) => new()
        {
            Timestamp = DateTimeOffset.Now,
            Direction = direction,
            RawHex = Convert.ToHexString(raw).ToLowerInvariant(),
            Sequence = sequence,
            DataType = dataType,
            CommandHex = payload.Length > 0 ? $"0x{payload[0]:x2}" : "none",
            PayloadHex = Convert.ToHexString(payload).ToLowerInvariant(),
        };

    private static IBuffer CreateBuffer(byte[] data)
    {
        using var writer = new DataWriter();
        writer.WriteBytes(data);
        return writer.DetachBuffer();
    }

    private static byte[] ReadBuffer(IBuffer buffer)
    {
        var bytes = new byte[(int)buffer.Length];
        using var reader = DataReader.FromBuffer(buffer);
        reader.ReadBytes(bytes);
        return bytes;
    }

    private async Task CleanupAsync(TandemSessionReport report)
    {
        if (_fromAcc is not null)
        {
            _fromAcc.ValueChanged -= OnFromAccValueChanged;
            try
            {
                await _fromAcc.WriteClientCharacteristicConfigurationDescriptorAsync(
                    GattClientCharacteristicConfigurationDescriptorValue.None);
            }
            catch
            {
                // Best effort.
            }
        }

        _service?.Dispose();
        _device?.Dispose();
        _service = null;
        _device = null;
    }
}























