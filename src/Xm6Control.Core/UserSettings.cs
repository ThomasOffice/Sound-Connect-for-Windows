using Xm6Control.Protocol;

namespace Xm6Control.Core;

public enum AppTheme
{
    System,
    Light,
    Dark,
}

public enum AppBackdrop
{
    Mica,
    Acrylic,
    Solid,
}

public enum TrayClickAction
{
    OpenWindow,
    ShowMenu,
}

/// <summary>
/// Local preferences for the desktop app. Every field has a usable default so a
/// missing or older settings file keeps working.
/// </summary>
public sealed class UserSettings
{
    public const int CurrentSchemaVersion = 1;
    public const string DefaultDeviceName = "WH-1000XM6";
    public const string ChineseLanguage = "zh-Hans";
    public const string EnglishLanguage = "en-US";

    private static readonly string[] KnownSections =
    [
        "overview",
        "noise",
        "eq",
        "listening",
        "multipoint",
        "autopower",
        "scenes",
        "settings",
        "about",
    ];

    private static readonly int[] AllowedRefreshMinutes = [0, 1, 5, 15];

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public bool CloseToTray { get; set; }

    public bool LaunchOnStartup { get; set; }

    public bool StartMinimized { get; set; }

    public bool RefreshOnStart { get; set; } = true;

    public bool RememberWindow { get; set; } = true;

    public int? WindowLeft { get; set; }

    public int? WindowTop { get; set; }

    public int? WindowWidth { get; set; }

    public int? WindowHeight { get; set; }

    public string LastSection { get; set; } = "overview";

    public AppTheme Theme { get; set; } = AppTheme.System;

    public AppBackdrop Backdrop { get; set; } = AppBackdrop.Mica;

    public string Language { get; set; } = ChineseLanguage;

    public bool TrayShowStatus { get; set; } = true;

    public TrayClickAction TrayClickAction { get; set; } = TrayClickAction.OpenWindow;

    /// <summary>Zero disables automatic refresh. One minute is the floor.</summary>
    public int AutoRefreshMinutes { get; set; }

    public bool AutoReconnect { get; set; } = true;

    public string DeviceName { get; set; } = DefaultDeviceName;

    public string? DeviceAddress { get; set; }

    public List<TandemSceneDefinition> Scenes { get; set; } = [];

    /// <summary>
    /// Repairs values that a hand-edited or partially written file could contain.
    /// Never throws.
    /// </summary>
    public void Normalize()
    {
        Theme = Enum.IsDefined(Theme) ? Theme : AppTheme.System;
        Backdrop = Enum.IsDefined(Backdrop) ? Backdrop : AppBackdrop.Mica;
        TrayClickAction = Enum.IsDefined(TrayClickAction)
            ? TrayClickAction
            : TrayClickAction.OpenWindow;

        if (!AllowedRefreshMinutes.Contains(AutoRefreshMinutes))
        {
            AutoRefreshMinutes = 0;
        }

        Language = NormalizeLanguage(Language);
        LastSection = KnownSections.Contains(LastSection, StringComparer.Ordinal)
            ? LastSection
            : "overview";

        WindowWidth = ClampOrNull(WindowWidth, WindowPlacement.MinWidth, 8000);
        WindowHeight = ClampOrNull(WindowHeight, WindowPlacement.MinHeight, 6000);
        if (WindowWidth is null || WindowHeight is null)
        {
            WindowWidth = null;
            WindowHeight = null;
            WindowLeft = null;
            WindowTop = null;
        }

        DeviceName = string.IsNullOrWhiteSpace(DeviceName)
            ? DefaultDeviceName
            : DeviceName.Trim();
        DeviceAddress = string.IsNullOrWhiteSpace(DeviceAddress)
            ? null
            : DeviceAddress.Trim();

        NormalizeScenes();
    }

    public static string NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return ChineseLanguage;
        }

        return language.Trim().StartsWith("en", StringComparison.OrdinalIgnoreCase)
            ? EnglishLanguage
            : ChineseLanguage;
    }

    private void NormalizeScenes()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var normalized = new List<TandemSceneDefinition>();
        foreach (var scene in Scenes)
        {
            if (scene is null || string.IsNullOrWhiteSpace(scene.Id) || string.IsNullOrWhiteSpace(scene.Name))
            {
                continue;
            }

            if (scene.NcAsm is null && scene.Eq is null && scene.Listening is null)
            {
                continue;
            }

            var id = scene.Id.Trim();
            var name = scene.Name.Trim();
            if (name.Length > 40)
            {
                name = name[..40];
            }

            if (!seen.Add(id))
            {
                continue;
            }

            normalized.Add(scene with
            {
                Id = id,
                Name = name,
                NcAsm = scene.NcAsm is { } ncasm
                    ? ncasm with { Level = (byte)Math.Clamp((int)ncasm.Level, 1, 20) }
                    : null,
            });

            if (normalized.Count >= 50)
            {
                break;
            }
        }

        Scenes = normalized;
    }

    private static int? ClampOrNull(int? value, int minimum, int maximum)
    {
        if (value is not { } number || number <= 0)
        {
            return null;
        }

        return Math.Clamp(number, minimum, maximum);
    }
}
