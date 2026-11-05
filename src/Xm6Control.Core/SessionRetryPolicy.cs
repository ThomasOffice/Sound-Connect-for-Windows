namespace Xm6Control.Core;

/// <summary>
/// Decides whether a failed Tandem session is worth retrying. A missing device
/// or a cancelled session is deterministic; retrying only delays the result.
/// </summary>
public static class SessionRetryPolicy
{
    public static bool ShouldRetry(string? status)
    {
        return status switch
        {
            null => false,
            "ok" => false,
            "not_found" => false,
            "cancelled" => false,
            "device_unavailable" => false,
            _ => true,
        };
    }
}
