using Xm6Control.Protocol;

namespace Xm6Control.Protocol.Tests;

public sealed class TandemV2Table1MessagesTests
{
    [Fact]
    public void BuildsConnectGetProtocolInfo()
    {
        Assert.Equal([0x00, 0x00], TandemV2Table1Messages.ConnectGetProtocolInfo());
    }

    [Fact]
    public void BuildsConnectGetDeviceInfo()
    {
        Assert.Equal(
            [0x04, 0x02],
            TandemV2Table1Messages.ConnectGetDeviceInfo(TandemDeviceInfoType.FirmwareVersion));
    }

    [Fact]
    public void BuildsNcOnOffSet()
    {
        Assert.Equal(
            [0x68, 0x01, 0x01, 0x01, 0x01],
            TandemV2Table1Messages.NcAsmSetNcOnOff(true));
    }

    [Fact]
    public void BuildsAutoPowerOffSet()
    {
        Assert.Equal(
            [0x28, 0x04, 0x01, 0x11],
            TandemV2Table1Messages.PowerSetAutoPowerOff(
                TandemAutoPowerOffElement.In30Minutes,
                TandemAutoPowerOffElement.Disable));
    }

    [Fact]
    public void BuildsPresetEqSet()
    {
        Assert.Equal(
            [0x58, 0x00, 0x01, 0x02, 0x0A, 0x0B],
            TandemV2Table1Messages.EqEbbSetPreset(
                TandemEqEbbInquiredType.PresetEq,
                TandemEqPresetId.Rock,
                [0x0A, 0x0B]));
    }

    [Fact]
    public void ParsesProtocolInfo()
    {
        var payload = new byte[] { 0x01, 0x00, 0x03, 0x00, 0x30, 0x01, 0x00, 0x01 };

        Assert.True(TandemV2Table1Messages.TryParseProtocolInfo(
            payload,
            out var version,
            out var tableSetFlag,
            out var secondFlag));
        Assert.Equal(0x03003001, version);
        Assert.Equal(0, tableSetFlag);
        Assert.Equal(1, secondFlag);
    }

    [Fact]
    public void ParsesDeviceInfoText()
    {
        var text = "WH-1000XM6"u8.ToArray();
        var payload = new byte[3 + text.Length];
        payload[0] = 0x05;
        payload[1] = 0x01;
        payload[2] = (byte)text.Length;
        text.CopyTo(payload.AsSpan(3));

        Assert.True(TandemV2Table1Messages.TryParseDeviceInfoText(
            payload,
            out var type,
            out var parsed));
        Assert.Equal(TandemDeviceInfoType.ModelName, type);
        Assert.Equal("WH-1000XM6", parsed);
    }
}
