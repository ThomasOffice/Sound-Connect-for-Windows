using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Xm6Control.Protocol;

namespace Xm6Control.Bluetooth.Windows;

public sealed class TandemClassicSession
{
    private static readonly Guid Table1Uuid = Guid.Parse("96CC203E-5068-46AD-B32D-E316F5E069BA");
    private static readonly Guid Table2Uuid = Guid.Parse("956C7B26-D49A-4BA8-B03F-B17D393CB6E2");

    private readonly object _writeGate = new();
    private readonly object _frameGate = new();
    private readonly List<byte> _receiveBuffer = [];
    private readonly object _pendingGate = new();
    private TaskCompletionSource<TandemFrame>? _pending;
    private HashSet<byte>? _pendingCommands;
    private byte _sequence;
    private TandemDataType _dataType = TandemDataType.DataMdr;
    private StreamSocket? _socket;
    private DataWriter? _writer;
    private TandemSessionReport? _report;
    private CancellationTokenSource? _readCts;

    public async Task<TandemSessionReport> RunAsync(
        ProbeRequest request,
        CancellationToken cancellationToken = default)
    {
        var report = new TandemSessionReport
        {
            StartedAt = DateTimeOffset.Now,
            Transport = "classic-rfcomm",
        };
        _report = report;

        RfcommDeviceService? connectedService = null;
        try
        {
            var device = await FindClassicDeviceAsync(request, report, cancellationToken);
            if (device is null)
            {
                report.Status = "not_found";
                report.Message = "No paired Classic Bluetooth device matched the target.";
                return report;
            }

            var classicDevice = await BluetoothDevice.FromIdAsync(device.Id);
            if (classicDevice is null)
            {
                report.Status = "classic_device_unavailable";
                report.Message = "BluetoothDevice.FromIdAsync returned null.";
                return report;
            }

            report.DeviceName = classicDevice.Name;
            report.DeviceId = classicDevice.DeviceId;
            report.Address = BluetoothAddress.Format(classicDevice.BluetoothAddress);

            foreach (var uuid in new[] { Table1Uuid, Table2Uuid })
            {
                var service = await FindServiceAsync(classicDevice, uuid, report, cancellationToken);
                if (service is null)
                {
                    continue;
                }

                connectedService = service;
                report.ClassicServiceUuid = uuid.ToString("D");
                _dataType = uuid == Table2Uuid ? TandemDataType.DataMdrNo2 : TandemDataType.DataMdr;
                report.Message = $"Connecting to RFCOMM service {uuid:D}.";
                break;
            }

            if (connectedService is null)
            {
                report.Status = "classic_service_not_found";
                report.Message =
                    "Neither Tandem Classic RFCOMM service UUID was found. "
                    + "The headphone may not expose SPP to this Windows host.";
                return report;
            }

            _socket = new StreamSocket();
            try
            {
                await _socket.ConnectAsync(
                    connectedService.ConnectionHostName,
                    connectedService.ConnectionServiceName,
                    SocketProtectionLevel.BluetoothEncryptionWithAuthentication);
            }
            catch (Exception ex)
            {
                report.Status = "classic_connect_failed";
                report.Message = $"{ex.GetType().Name}: {ex.Message}";
                return report;
            }

            _writer = new DataWriter(_socket.OutputStream);
            _readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _ = ReadLoopAsync(_readCts.Token);

            await RunHandshakeAsync(report, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            report.Status = "cancelled";
            report.Message = "Classic Tandem session was cancelled.";
        }
        catch (Exception ex)
        {
            report.Status = "error";
            report.Message = $"{ex.GetType().Name}: {ex.Message}";
            report.Errors.Add(ex.ToString());
        }
        finally
        {
            await CleanupAsync(connectedService);
            report.CompletedAt = DateTimeOffset.Now;
        }

        return report;
    }

    private static async Task<DeviceInformation?> FindClassicDeviceAsync(
        ProbeRequest request,
        TandemSessionReport report,
        CancellationToken cancellationToken)
    {
        var devices = await DeviceInformation.FindAllAsync(
            BluetoothDevice.GetDeviceSelectorFromPairingState(true));

        var matches = devices
            .Where(device => Matches(device, request))
            .OrderByDescending(device => AddressMatches(device, request.Address))
            .ToList();

        if (matches.Count > 1)
        {
            report.Warnings.Add($"Multiple Classic devices matched; using '{matches[0].Name}'.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        return matches.FirstOrDefault();
    }

    private static bool Matches(DeviceInformation device, ProbeRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Name)
            && (device.Name ?? string.Empty).Contains(request.Name, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return AddressMatches(device, request.Address);
    }

    private static bool AddressMatches(DeviceInformation device, string? requestedAddress)
    {
        if (string.IsNullOrWhiteSpace(requestedAddress))
        {
            return false;
        }

        return BluetoothAddress.Normalize(device.Id) == BluetoothAddress.Normalize(requestedAddress);
    }

    private static async Task<RfcommDeviceService?> FindServiceAsync(
        BluetoothDevice device,
        Guid uuid,
        TandemSessionReport report,
        CancellationToken cancellationToken)
    {
        try
        {
            var serviceId = RfcommServiceId.FromUuid(uuid);
            var selector = RfcommDeviceService.GetDeviceSelectorForBluetoothDeviceAndServiceId(
                device,
                serviceId);
            var services = await DeviceInformation.FindAllAsync(selector);
            var serviceInfo = services.FirstOrDefault();

            if (serviceInfo is null)
            {
                report.Warnings.Add(
                    $"RFCOMM service {uuid:D} is not exposed by '{device.Name}'.");
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();
            return await RfcommDeviceService.FromIdAsync(serviceInfo.Id);
        }
        catch (Exception ex)
        {
            report.Warnings.Add($"RFCOMM {uuid:D} lookup failed: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private async Task RunHandshakeAsync(TandemSessionReport report, CancellationToken cancellationToken)
    {
        if (string.Equals(report.ClassicServiceUuid, Table2Uuid.ToString("D"), StringComparison.OrdinalIgnoreCase))
        {
            await RunTable2HandshakeAsync(report, cancellationToken);
            return;
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
            report.Handshake.CapabilityPayloadHex =
                Convert.ToHexString(capability.Payload).ToLowerInvariant();
        }

        var support = await SendAndWaitAsync(
            TandemV2Table1Messages.ConnectGetSupportFunction(),
            [(byte)TandemV2Table1Command.ConnectRetSupportFunction],
            report,
            cancellationToken);
        if (support is not null
            && TandemV2Table1Messages.TryParseSupportFunctions(support.Payload, out var functions))
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

        report.Status = report.Handshake.ProtocolVersion is not null ? "ok" : "handshake_incomplete";
        report.Message = report.Status == "ok"
            ? "Classic Tandem handshake completed."
            : "Classic Tandem connected but the handshake did not complete.";
    }

    private async Task RunTable2HandshakeAsync(
        TandemSessionReport report,
        CancellationToken cancellationToken)
    {
        var support = await SendAndWaitAsync(
            TandemV2Table1Messages.ConnectGetSupportFunction(),
            [(byte)TandemV2Table1Command.ConnectRetSupportFunction],
            report,
            cancellationToken);

        if (support is not null
            && TandemV2Table1Messages.TryParseSupportFunctions(support.Payload, out var functions))
        {
            report.Handshake.SupportFunctions = functions
                .Select(function => new TandemFunction
                {
                    FunctionType = function.FunctionType,
                    TableNumber = function.TableNumber,
                })
                .ToList();
        }

        report.Status = support is not null ? "ok" : "handshake_incomplete";
        report.Message = support is not null
            ? "Classic Tandem table2 support-function handshake completed."
            : "Classic Tandem table2 connected but the support-function request did not complete.";
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
        CancellationToken cancellationToken)
    {
        if (_writer is null)
        {
            return null;
        }

        var sequence = _sequence++;
        var raw = TandemFrameCodec.Encode(sequence, _dataType, payload);
        var completion = new TaskCompletionSource<TandemFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_pendingGate)
        {
            _pending = completion;
            _pendingCommands = expectedCommands.ToHashSet();
        }

        try
        {
            await WriteAsync(raw);
            report.Frames.Add(RecordFrame("tx", raw, sequence, _dataType, payload));
        }
        catch (Exception ex)
        {
            report.Warnings.Add($"Classic Tandem write failed: {ex.GetType().Name}: {ex.Message}");
            ClearPending();
            return null;
        }

        var completed = await Task.WhenAny(
            completion.Task,
            Task.Delay(TimeSpan.FromMilliseconds(5_000), cancellationToken));
        if (completed == completion.Task)
        {
            return await completion.Task;
        }

        report.Warnings.Add($"Classic Tandem response timed out for command 0x{payload[0]:x2}.");
        ClearPending();
        return null;
    }

    private async Task WriteAsync(byte[] data)
    {
        if (_writer is null)
        {
            throw new InvalidOperationException("Writer is not initialized.");
        }

        lock (_writeGate)
        {
            _writer.WriteBytes(data);
        }

        await _writer.StoreAsync();
        await _writer.FlushAsync();
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        if (_socket is null)
        {
            return;
        }

        using var reader = new DataReader(_socket.InputStream)
        {
            InputStreamOptions = InputStreamOptions.Partial,
        };

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var count = await reader.LoadAsync(4_096);
                if (count == 0)
                {
                    break;
                }

                var data = new byte[count];
                reader.ReadBytes(data);
                HandleIncoming(data);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during cleanup.
        }
        catch (Exception ex)
        {
            _report?.Warnings.Add($"Classic read loop ended: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void HandleIncoming(byte[] data)
    {
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
            _report?.Frames.Add(RecordFrame(
                "rx",
                TandemFrameCodec.Encode(frame.Sequence, frame.DataType, frame.Payload),
                frame.Sequence,
                frame.DataType,
                frame.Payload));

            if (frame.DataType != TandemDataType.Ack)
            {
                _ = WriteAsync(TandemFrameCodec.EncodeAck(frame.Sequence));
            }

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

    private async Task CleanupAsync(RfcommDeviceService? service)
    {
        _readCts?.Cancel();
        _readCts?.Dispose();
        _readCts = null;

        if (_writer is not null)
        {
            try
            {
                _writer.DetachStream();
                _writer.Dispose();
            }
            catch
            {
                // Best effort.
            }

            _writer = null;
        }

        if (_socket is not null)
        {
            try
            {
                _socket.Dispose();
            }
            catch
            {
                // Best effort.
            }

            _socket = null;
        }

        service?.Dispose();
        await Task.CompletedTask;
    }
}








