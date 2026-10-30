using System.Text.RegularExpressions;

namespace Xm6Control.Protocol;

public static partial class Redaction
{
    [GeneratedRegex(@"(?i)\b[0-9a-f]{2}(?:[:-][0-9a-f]{2}){5}\b")]
    private static partial Regex SeparatedMac();

    [GeneratedRegex(@"(?i)\b[0-9a-f]{12}\b")]
    private static partial Regex BareMac();

    [GeneratedRegex(@"(?i)\b(BluetoothLE?#)[^\s""']+")]
    private static partial Regex BluetoothDeviceId();

    [GeneratedRegex(@"(?i)(DEV_)([0-9a-f]{12})")]
    private static partial Regex DevAddress();

    [GeneratedRegex(@"(?i)\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b")]
    private static partial Regex Uuid();

    public static string RedactMac(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value ?? string.Empty;
        }

        var uuids = new List<string>();
        var protectedValue = Uuid().Replace(value, match =>
        {
            uuids.Add(match.Value);
            return $"__UUID_{uuids.Count - 1}__";
        });

        var redacted = BluetoothDeviceId().Replace(protectedValue, "$1[redacted]");
        redacted = DevAddress().Replace(redacted, "$1************");
        redacted = SeparatedMac().Replace(redacted, "**:**:**:**:**:**");
        redacted = BareMac().Replace(redacted, "************");

        for (var index = 0; index < uuids.Count; index++)
        {
            redacted = redacted.Replace($"__UUID_{index}__", uuids[index], StringComparison.Ordinal);
        }

        return redacted;
    }
}
