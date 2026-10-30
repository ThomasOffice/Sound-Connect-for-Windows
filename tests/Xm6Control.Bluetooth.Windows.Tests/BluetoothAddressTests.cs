using Xm6Control.Bluetooth.Windows;

namespace Xm6Control.Bluetooth.Windows.Tests;

public sealed class BluetoothAddressTests
{
    [Theory]
    [InlineData("AA:BB:CC:DD:EE:FF", 0xAABBCCDDEEFFUL)]
    [InlineData("AABBCCDDEEFF", 0xAABBCCDDEEFFUL)]
    [InlineData("Bluetooth#Bluetooth40:11:22:33:44:55:AA:BB:CC:DD:EE:FF", 0xAABBCCDDEEFFUL)]
    [InlineData("BTHENUM\\DEV_AABBCCDDEEFF\\7&2B3B0905&1", 0xAABBCCDDEEFFUL)]
    public void ParsesRemoteAddress(string input, ulong expected)
    {
        Assert.True(BluetoothAddress.TryParse(input, out var address));
        Assert.Equal(expected, address);
    }

    [Fact]
    public void RejectsTooShortInput()
    {
        Assert.False(BluetoothAddress.TryParse("5818", out _));
    }

    [Fact]
    public void FormatsAddress()
    {
        Assert.Equal("AA:BB:CC:DD:EE:FF", BluetoothAddress.Format(0xAABBCCDDEEFFUL));
    }

    [Fact]
    public void NormalizesToLastSixBytes()
    {
        Assert.Equal(
            "AABBCCDDEEFF",
            BluetoothAddress.Normalize("BluetoothLE#BluetoothLE40:11:22:33:44:55:AA:BB:CC:DD:EE:FF"));
    }
}
