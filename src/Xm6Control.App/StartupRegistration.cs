using Microsoft.Win32;
using Xm6Control.Core;

namespace Xm6Control.App;

/// <summary>
/// Per-user start-up registration through the HKCU Run key. The app is
/// unpackaged, so this avoids the Start-up Task API and never needs elevation.
/// </summary>
internal static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Xm6Control";

    public static string? ExecutablePath => Environment.ProcessPath;

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            var value = key?.GetValue(ValueName) as string;
            var executable = ExecutablePath;
            return executable is not null && StartupCommand.Matches(value, executable);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return false;
        }
    }

    public static bool TrySet(bool enabled, out string? error)
    {
        error = null;
        try
        {
            if (enabled)
            {
                var executable = ExecutablePath;
                if (executable is null)
                {
                    error = Localization.Text("status.startup.nopath");
                    return false;
                }

                using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, true);
                key.SetValue(ValueName, StartupCommand.Build(executable), RegistryValueKind.String);
                return true;
            }

            using var existing = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            existing?.DeleteValue(ValueName, throwOnMissingValue: false);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            error = ex.Message;
            return false;
        }
    }
}
