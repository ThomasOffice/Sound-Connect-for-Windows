using Xm6Control.Protocol;

namespace Xm6Control.Protocol.Tests;

public sealed class TandemFrameCodecTests
{
    [Fact]
    public void EncodesConnectGetProtocolInfo()
    {
        var frame = TandemFrameCodec.Encode(
            0,
            TandemDataType.DataMdr,
            TandemV2Table1Messages.ConnectGetProtocolInfo());

        Assert.Equal("3e0c000000000200000e3c", Convert.ToHexString(frame).ToLowerInvariant());
    }

    [Theory]
    [InlineData(0, "3e010000000000013c")]
    [InlineData(1, "3e010100000000023c")]
    public void EncodesAck(byte sequence, string expectedHex)
    {
        var frame = TandemFrameCodec.EncodeAck(sequence);
        Assert.Equal(expectedHex, Convert.ToHexString(frame).ToLowerInvariant());
    }

    [Fact]
    public void RoundTripsEscapedPayload()
    {
        var payload = new byte[] { 0x3C, 0x3D, 0x3E, 0x00, 0x10 };
        var raw = TandemFrameCodec.Encode(7, TandemDataType.DataMdr, payload);

        Assert.True(TandemFrameCodec.TryDecode(raw, out var frame, out var error), error);
        Assert.NotNull(frame);
        Assert.Equal(7, frame.Sequence);
        Assert.Equal(TandemDataType.DataMdr, frame.DataType);
        Assert.Equal(payload, frame.Payload);
    }

    [Fact]
    public void RejectsBadChecksum()
    {
        var raw = TandemFrameCodec.Encode(
            0,
            TandemDataType.DataMdr,
            TandemV2Table1Messages.ConnectGetProtocolInfo());
        raw[^2] ^= 0xFF;

        Assert.False(TandemFrameCodec.TryDecode(raw, out _, out var error));
        Assert.Contains("Checksum", error);
    }

    [Fact]
    public void DecodesMultipleFrames()
    {
        var first = TandemFrameCodec.Encode(0, TandemDataType.DataMdr, [0x00, 0x00]);
        var second = TandemFrameCodec.EncodeAck(0);
        var combined = first.Concat(second).ToArray();

        var frames = TandemFrameCodec.DecodeAll(combined);

        Assert.Equal(2, frames.Count);
        Assert.Equal(TandemDataType.DataMdr, frames[0].DataType);
        Assert.Equal(TandemDataType.Ack, frames[1].DataType);
    }
}




