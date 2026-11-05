namespace Xm6Control.Core;

/// <summary>
/// Pure helpers for the per-user Run key value. Keeping the quoting rules out
/// of the registry code makes them testable without touching HKCU.
/// </summary>
public static class StartupCommand
{
    public static string Build(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("Executable path must not be empty.", nameof(executablePath));
        }

        return $"\"{executablePath.Trim().Trim('"')}\"";
    }

    /// <summary>Returns the executable path from a Run value, quoted or not.</summary>
    public static string? ExtractPath(string? storedValue)
    {
        if (string.IsNullOrWhiteSpace(storedValue))
        {
            return null;
        }

        var value = storedValue.Trim();
        if (value.StartsWith('"'))
        {
            var closing = value.IndexOf('"', 1);
            value = closing < 0 ? value[1..] : value[1..closing];
        }

        value = value.Trim();
        return value.Length == 0 ? null : value;
    }

    public static bool Matches(string? storedValue, string executablePath)
    {
        var stored = ExtractPath(storedValue);
        if (stored is null || string.IsNullOrWhiteSpace(executablePath))
        {
            return false;
        }

        try
        {
            return string.Equals(
                Path.GetFullPath(stored),
                Path.GetFullPath(executablePath),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }
}
