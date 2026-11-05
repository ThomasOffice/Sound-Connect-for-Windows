namespace Xm6Control.Core.Tests;

public sealed class SessionRetryPolicyTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("ok")]
    [InlineData("not_found")]
    [InlineData("cancelled")]
    [InlineData("device_unavailable")]
    public void Terminal_statuses_are_not_retried(string? status)
    {
        Assert.False(SessionRetryPolicy.ShouldRetry(status));
    }

    [Theory]
    [InlineData("error")]
    [InlineData("gatt_unreachable")]
    [InlineData("handshake_incomplete")]
    [InlineData("tandem_service_missing")]
    [InlineData("tandem_service_unreadable")]
    [InlineData("tandem_characteristic_missing")]
    public void Transient_statuses_are_retried(string status)
    {
        Assert.True(SessionRetryPolicy.ShouldRetry(status));
    }
}
