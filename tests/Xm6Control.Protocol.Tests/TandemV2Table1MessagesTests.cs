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

    [Fact]
    public void BuildsCommonGetStatus()
    {
        Assert.Equal(
            [0x12, 0x02],
            TandemV2Table1Messages.CommonGetStatus(TandemCommonInquiredType.AudioCodec));
        Assert.Equal(
            [0x12, 0x03],
            TandemV2Table1Messages.CommonGetStatus(TandemCommonInquiredType.UpscalingEffect));
    }

    [Fact]
    public void BuildsAudioSetUpscaling()
    {
        Assert.Equal([0xE8, 0x01, 0x01], TandemV2Table1Messages.AudioSetUpscaling(true));
        Assert.Equal([0xE8, 0x01, 0x00], TandemV2Table1Messages.AudioSetUpscaling(false));
    }

    [Theory]
    [InlineData(1, TandemAudioCodec.Sbc)]
    [InlineData(2, TandemAudioCodec.Aac)]
    [InlineData(3, TandemAudioCodec.Ldac)]
    [InlineData(32, TandemAudioCodec.AptX)]
    [InlineData(33, TandemAudioCodec.AptXHd)]
    [InlineData(48, TandemAudioCodec.Lc3)]
    [InlineData(0, TandemAudioCodec.Unsettled)]
    [InlineData(7, TandemAudioCodec.Other)]
    public void ParsesAudioCodec(byte value, TandemAudioCodec expected)
    {
        var payload = new byte[] { 0x13, 0x02, value };

        Assert.True(TandemV2Table1Messages.TryParseAudioCodec(payload, out var codec));
        Assert.Equal(expected, codec);
    }

    [Fact]
    public void ParsesUpscalingEffect()
    {
        var payload = new byte[] { 0x13, 0x03, 0x02, 0x01 };

        Assert.True(TandemV2Table1Messages.TryParseUpscalingEffect(
            payload,
            out var status,
            out var type));
        Assert.Equal(TandemUpscalingType.DseeHxAi, type);
        Assert.Equal(TandemUpscalingEffectStatus.Valid, status);
    }

    [Fact]
    public void RejectsUpscalingEffectWithUnknownType()
    {
        var payload = new byte[] { 0x13, 0x03, 0x09, 0x01 };

        Assert.False(TandemV2Table1Messages.TryParseUpscalingEffect(
            payload,
            out _,
            out _));
    }

    [Fact]
    public void ParsesAudioUpscalingSetting()
    {
        Assert.True(TandemV2Table1Messages.TryParseAudioUpscalingParam(
            new byte[] { 0xE7, 0x01, 0x01 },
            out var setting));
        Assert.Equal(TandemUpscalingSetting.Auto, setting);

        Assert.True(TandemV2Table1Messages.TryParseAudioUpscalingParam(
            new byte[] { 0xE9, 0x01, 0x00 },
            out setting));
        Assert.Equal(TandemUpscalingSetting.Off, setting);
    }

    [Fact]
    public void ParsesInvertedAudioUpscalingStatus()
    {
        Assert.True(TandemV2Table1Messages.TryParseAudioUpscalingStatus(
            new byte[] { 0xE3, 0x01, 0x00 },
            out var state));
        Assert.Equal(TandemEnableState.Enable, state);

        Assert.True(TandemV2Table1Messages.TryParseAudioUpscalingStatus(
            new byte[] { 0xE3, 0x01, 0x01 },
            out state));
        Assert.Equal(TandemEnableState.Disable, state);
    }

    [Fact]
    public void ParsesAudioUpscalingCapability()
    {
        Assert.True(TandemV2Table1Messages.TryParseAudioUpscalingCapability(
            new byte[] { 0xE1, 0x01, 0x02 },
            out var type));
        Assert.Equal(TandemUpscalingType.DseeHxAi, type);
    }
}
