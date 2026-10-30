using Xm6Control.Protocol;

namespace Xm6Control.Protocol.Tests;

public sealed class ByteFormattingTests
{
    [Fact]
    public void FormatsHexWithoutSeparators()
    {
        Assert.Equal("00ff10", ByteFormatting.ToHex([0x00, 0xff, 0x10]));
    }

    [Fact]
    public void FormatsSpacedHex()
    {
        Assert.Equal("00 ff 10", ByteFormatting.ToSpacedHex([0x00, 0xff, 0x10]));
    }

    [Fact]
    public void DescribesPrintableUtf8AsText()
    {
        Assert.Equal("WH-1000XM6", ByteFormatting.Describe("WH-1000XM6"u8));
    }

    [Fact]
    public void DescribesBinaryAsSpacedHex()
    {
        Assert.Equal("00 01 ff", ByteFormatting.Describe([0x00, 0x01, 0xff]));
    }
}
