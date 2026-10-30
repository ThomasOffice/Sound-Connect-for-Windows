using Xm6Control.Protocol;

namespace Xm6Control.Bluetooth.Windows;

public sealed class TandemSessionReport
{
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset CompletedAt { get; set; }
    public string Status { get; set; } = "unknown";
    public string Transport { get; set; } = "unknown";
    public string? ClassicServiceUuid { get; set; }
    public string? Message { get; set; }
    public string? DeviceName { get; set; }
    public string? DeviceId { get; set; }
    public string? Address { get; set; }
    public bool TandemServicePresent { get; set; }
    public bool PairingBootstrapAttempted { get; set; }
    public string? PairingBootstrapResult { get; set; }
    public List<GattServiceBrief> Services { get; set; } = [];
    public TandemHandshakeInfo Handshake { get; set; } = new();
    public List<TandemFrameRecord> Frames { get; set; } = [];
    public List<TandemFeatureQuery> FeatureQueries { get; set; } = [];
    public TandemEqChange? Eq { get; set; }
    public TandemNcAsmChange? NcAsm { get; set; }
    public TandemMultipointChange? Multipoint { get; set; }
    public TandemAutoPowerOffChange? AutoPowerOff { get; set; }
    public TandemListeningModeChange? ListeningMode { get; set; }
    public TandemDeviceSnapshot? Snapshot { get; set; }
    public List<string> Warnings { get; set; } = [];
    public List<string> Errors { get; set; } = [];
}

public sealed class TandemDeviceSnapshot
{
    public int? BatteryPercent { get; set; }
    public byte? BatteryStatus { get; set; }
    public TandemNcAsmState? NcAsm { get; set; }
    public TandemEqState? Eq { get; set; }
    public bool? MultipointEnabled { get; set; }
    public TandemAutoPowerOffState? AutoPowerOff { get; set; }
    public IReadOnlyList<TandemAutoPowerOffElement>? AutoPowerOffElements { get; set; }
    public TandemListeningModeState? ListeningMode { get; set; }
}

public sealed class TandemNcAsmChange
{
    public string Status { get; set; } = "unknown";
    public TandemNcAsmState? Before { get; set; }
    public TandemNcAsmState? Requested { get; set; }
    public TandemNcAsmState? After { get; set; }
    public string? ResponseHex { get; set; }
    public bool Verified { get; set; }
}

public sealed class TandemEqChange
{
    public string Status { get; set; } = "unknown";
    public TandemEqState? Before { get; set; }
    public TandemEqState? Requested { get; set; }
    public TandemEqState? After { get; set; }
    public string? ResponseHex { get; set; }
    public bool UsedPresetDefault { get; set; }
    public bool Verified { get; set; }
}

public sealed class TandemEqState
{
    public TandemEqPresetId Preset { get; set; }
    public byte[] Bands { get; set; } = [];
    public string? RawHex { get; set; }
}

public sealed class TandemMultipointChange
{
    public string Status { get; set; } = "unknown";
    public bool? Before { get; set; }
    public bool? Requested { get; set; }
    public bool? After { get; set; }
    public string? ResponseHex { get; set; }
    public bool Verified { get; set; }
}

public sealed class TandemAutoPowerOffChange
{
    public string Status { get; set; } = "unknown";
    public TandemAutoPowerOffState? Before { get; set; }
    public TandemAutoPowerOffState? Requested { get; set; }
    public TandemAutoPowerOffState? After { get; set; }
    public IReadOnlyList<TandemAutoPowerOffElement>? Elements { get; set; }
    public string? ResponseHex { get; set; }
    public bool Verified { get; set; }
}

public sealed class TandemAutoPowerOffSnapshot
{
    public TandemAutoPowerOffState? State { get; set; }
    public IReadOnlyList<TandemAutoPowerOffElement> Elements { get; set; } = [];
}

public sealed class TandemListeningModeChange
{
    public string Status { get; set; } = "unknown";
    public TandemListeningModeState? Before { get; set; }
    public TandemListeningModeState? Requested { get; set; }
    public TandemListeningModeState? After { get; set; }
    public string? ResponseHex { get; set; }
    public bool Verified { get; set; }
}

public sealed class TandemFeatureQuery
{
    public string Name { get; set; } = string.Empty;
    public string CommandHex { get; set; } = string.Empty;
    public byte InquiredType { get; set; }
    public string Status { get; set; } = "not_answered";
    public string? PayloadHex { get; set; }
    public string? Detail { get; set; }
}

public sealed class GattServiceBrief
{
    public string Uuid { get; set; } = string.Empty;
    public string? Name { get; set; }
    public int CharacteristicCount { get; set; }
}

public sealed class TandemHandshakeInfo
{
    public int? ProtocolVersion { get; set; }
    public byte? TableSetFlag { get; set; }
    public byte? SecondFlag { get; set; }
    public string? Model { get; set; }
    public string? Firmware { get; set; }
    public string? CapabilityPayloadHex { get; set; }
    public List<TandemFunction> SupportFunctions { get; set; } = [];
    public int? WritableValueLength { get; set; }
    public int? DeterminedMtu { get; set; }
}

public sealed class TandemFunction
{
    public byte FunctionType { get; set; }
    public byte TableNumber { get; set; }
}

public sealed class TandemFrameRecord
{
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
    public string Direction { get; set; } = string.Empty;
    public string RawHex { get; set; } = string.Empty;
    public byte Sequence { get; set; }
    public TandemDataType DataType { get; set; }
    public string CommandHex { get; set; } = string.Empty;
    public string PayloadHex { get; set; } = string.Empty;
    public string? DecodedText { get; set; }
}








