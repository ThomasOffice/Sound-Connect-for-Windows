using System.Globalization;
using System.Text.RegularExpressions;

namespace Xm6Control.Bluetooth.Windows;

public static partial class BluetoothAddress
{
    [GeneratedRegex(@"(?i)DEV_([0-9a-f]{12})")]
    private static partial Regex DevAddress();

    public static bool TryParse(string? value, out ulong address)
    {
        address = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var devMatch = DevAddress().Match(value);
        if (devMatch.Success)
        {
            return ulong.TryParse(
                devMatch.Groups[1].Value,
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out address);
        }

        var hex = new string(value.Where(Uri.IsHexDigit).ToArray());
        if (hex.Length < 12)
        {
            return false;
        }

        // DeviceInformation IDs contain the local adapter address before the
        // remote device address. The remote address is the final 12 hex digits.
        var remote = hex[^12..];
        return ulong.TryParse(remote, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out address);
    }

    public static string Format(ulong address)
    {
        var hex = address.ToString("X12", CultureInfo.InvariantCulture);
        return string.Join(':', Enumerable.Range(0, 6).Select(index => hex.Substring(index * 2, 2)));
    }

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var devMatch = DevAddress().Match(value);
        if (devMatch.Success)
        {
            return devMatch.Groups[1].Value.ToUpperInvariant();
        }

        var hex = new string(value.Where(Uri.IsHexDigit).ToArray());
        return hex.Length >= 12 ? hex[^12..].ToUpperInvariant() : null;
    }
}
