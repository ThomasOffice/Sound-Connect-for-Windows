namespace Xm6Control.Bluetooth.Windows;

internal static class GattUuidNames
{
    private static readonly Dictionary<Guid, string> Names = new()
    {
        [Uuid(0x1800)] = "Generic Access",
        [Uuid(0x1801)] = "Generic Attribute",
        [Uuid(0x180A)] = "Device Information",
        [Uuid(0x180F)] = "Battery",
        [Uuid(0x1812)] = "Human Interface Device",
        [Uuid(0x181A)] = "Environmental Sensing",
        [Uuid(0x184E)] = "Audio Input Control",
        [Uuid(0x2A19)] = "Battery Level",
        [Uuid(0x2A24)] = "Model Number",
        [Uuid(0x2A25)] = "Serial Number",
        [Uuid(0x2A26)] = "Firmware Revision",
        [Uuid(0x2A27)] = "Hardware Revision",
        [Uuid(0x2A28)] = "Software Revision",
        [Uuid(0x2A29)] = "Manufacturer Name",
        [Uuid(0x2A50)] = "PnP ID",
    };

    public static string? GetName(Guid uuid) => Names.GetValueOrDefault(uuid);

    public static bool Is(Guid uuid, ushort shortUuid) => uuid == Uuid(shortUuid);

    public static Guid Uuid(ushort shortUuid) =>
        new($"0000{shortUuid:x4}-0000-1000-8000-00805f9b34fb");
}
