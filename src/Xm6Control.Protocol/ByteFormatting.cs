using System.Globalization;

namespace Xm6Control.Protocol;

public static class ByteFormatting
{
    public static string ToHex(ReadOnlySpan<byte> data) =>
        Convert.ToHexString(data).ToLowerInvariant();

    public static string ToSpacedHex(ReadOnlySpan<byte> data) =>
        string.Join(' ', data.ToArray().Select(static value => value.ToString("x2", CultureInfo.InvariantCulture)));

    public static bool LooksPrintable(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return false;
        }

        var printable = 0;
        foreach (var value in data)
        {
            if (value is 0x09 or 0x0a or 0x0d || value is >= 0x20 and <= 0x7e)
            {
                printable++;
            }
        }

        return printable >= data.Length * 0.9;
    }

    public static string Describe(ReadOnlySpan<byte> data)
    {
        if (LooksPrintable(data))
        {
            return System.Text.Encoding.UTF8.GetString(data);
        }

        return ToSpacedHex(data);
    }
}
