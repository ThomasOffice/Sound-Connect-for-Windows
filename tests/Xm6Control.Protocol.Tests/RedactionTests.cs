using Xm6Control.Protocol;

namespace Xm6Control.Protocol.Tests;

public sealed class RedactionTests
{
    [Theory]
    [InlineData("AA:BB:CC:DD:EE:FF", "**:**:**:**:**:**")]
    [InlineData("AABBCCDDEEFF", "************")]
    [InlineData("BTHENUM\\DEV_AABBCCDDEEFF\\7&2B3B0905&1", "BTHENUM\\DEV_************\\7&2B3B0905&1")]
    [InlineData(
        "BluetoothLE#BluetoothLE40:11:22:33:44:55:AA:BB:CC:DD:EE:FF",
        "BluetoothLE#[redacted]")]
    [InlineData("no address here", "no address here")]
    [InlineData(
        "0000180a-0000-1000-8000-00805f9b34fb",
        "0000180a-0000-1000-8000-00805f9b34fb")]
    public void RedactsBluetoothAddresses(string input, string expected)
    {
        Assert.Equal(expected, Redaction.RedactMac(input));
    }
}
