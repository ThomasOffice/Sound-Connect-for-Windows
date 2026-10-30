using Xm6Control.Protocol;

namespace Xm6Control.Bluetooth.Windows;

public sealed class ProbeRequest
{
    public string Name { get; init; } = "WH-1000XM6";
    public string? Address { get; init; }
    public int NotificationWaitMs { get; init; } = 5_000;
    public string Transport { get; init; } = "auto";
    public string AddressType { get; init; } = "auto";
    public bool BootstrapTandem { get; init; }
    public bool QueryFeatures { get; init; }
    public bool QuerySnapshot { get; init; }
    public bool ApplyEq { get; init; }
    public TandemEqPresetId? EqPreset { get; init; }
    public IReadOnlyList<byte>? EqBands { get; init; }
    public bool ApplyNcAsm { get; init; }
    public TandemNcAsmMode? NcAsmMode { get; init; }
    public byte NcAsmLevel { get; init; } = 20;
    public bool NcAsmEnabled { get; init; } = true;
    public bool NcAsmVoiceFocus { get; init; }
    public bool NcAsmNoiseAdaptive { get; init; }
    public TandemNoiseAdaptiveSensitivity NcAsmSensitivity { get; init; } =
        TandemNoiseAdaptiveSensitivity.Standard;
    public bool ApplyMultipoint { get; init; }
    public bool? MultipointEnabled { get; init; }
    public bool ApplyAutoPowerOff { get; init; }
    public TandemAutoPowerOffElement? AutoPowerOffElement { get; init; }
    public bool ApplyListeningMode { get; init; }
    public TandemListeningMode? ListeningMode { get; init; }
    public TandemRoomSize? BgmRoomSize { get; init; }
}

public sealed class ProbeReport
{
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset CompletedAt { get; set; }
    public string Status { get; set; } = "unknown";
    public string? Message { get; set; }
    public string RequestedName { get; set; } = string.Empty;
    public string? RequestedAddress { get; set; }
    public string Transport { get; set; } = "auto";
    public List<DeviceCandidate> Candidates { get; set; } = [];
    public GattProbeResult? Gatt { get; set; }
    public RfcommProbeResult? Rfcomm { get; set; }
    public List<string> Warnings { get; set; } = [];
    public List<string> Errors { get; set; } = [];
}

public sealed class DeviceCandidate
{
    public string Name { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string? Address { get; set; }
    public bool IsPaired { get; set; }
    public bool IsConnected { get; set; }
}

public sealed class GattProbeResult
{
    public string DeviceName { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string? Address { get; set; }
    public bool IsPaired { get; set; }
    public string ConnectionStatus { get; set; } = "unknown";
    public string? ConnectionError { get; set; }
    public GattDeviceFacts Facts { get; set; } = new();
    public List<GattServiceInfo> Services { get; set; } = [];
}

public sealed class GattDeviceFacts
{
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? Firmware { get; set; }
    public string? Hardware { get; set; }
    public string? Software { get; set; }
    public string? SerialNumber { get; set; }
    public int? BatteryPercent { get; set; }
}

public sealed class GattServiceInfo
{
    public string Uuid { get; set; } = string.Empty;
    public string? Name { get; set; }
    public List<GattCharacteristicInfo> Characteristics { get; set; } = [];
}

public sealed class GattCharacteristicInfo
{
    public string Uuid { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string Properties { get; set; } = string.Empty;
    public string? ReadStatus { get; set; }
    public int ValueLength { get; set; }
    public string? ValueHex { get; set; }
    public string? ValueText { get; set; }
    public string? CccdStatus { get; set; }
    public int NotificationCount { get; set; }
    public List<string> Notifications { get; set; } = [];
}

public sealed class RfcommProbeResult
{
    public string? ConnectStatus { get; set; }
    public List<RfcommServiceInfo> Services { get; set; } = [];
}

public sealed class RfcommServiceInfo
{
    public string Name { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Uuid { get; set; } = string.Empty;
    public string? ServiceName { get; set; }
}







