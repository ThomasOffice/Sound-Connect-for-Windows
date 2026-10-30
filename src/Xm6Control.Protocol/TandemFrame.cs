namespace Xm6Control.Protocol;

public enum TandemDataType : byte
{
    Data = 0,
    Ack = 1,
    DataMcNo1 = 2,
    DataSsh = 7,
    DataIcd = 9,
    DataEv = 10,
    DataMdr = 12,
    DataCommon = 13,
    DataMdrNo2 = 14,
    Shot = 16,
    ShotMcNo1 = 18,
    ShotIcd = 25,
    ShotEv = 26,
    ShotMdr = 28,
    ShotCommon = 29,
    ShotMdrNo2 = 30,
    LargeDataSsh = 39,
    LargeDataMdr = 44,
    LargeDataCommon = 45,
    Unknown = 255,
}

public sealed record TandemFrame(
    byte Sequence,
    TandemDataType DataType,
    byte[] Payload,
    byte Checksum)
{
    public byte Command => Payload.Length > 0 ? Payload[0] : (byte)0;

    public string CommandHex => Payload.Length > 0 ? $"0x{Payload[0]:x2}" : "none";
}
