using System.Text.Json;
using Xm6Control.Core;
using Xm6Control.Protocol;

namespace Xm6Control.Core.Tests;

public sealed class UserSettingsTests
{
    [Fact]
    public void Defaults_are_usable()
    {
        var settings = new UserSettings();

        Assert.False(settings.CloseToTray);
        Assert.True(settings.RememberWindow);
        Assert.True(settings.RefreshOnStart);
        Assert.True(settings.AutoReconnect);
        Assert.Equal(0, settings.AutoRefreshMinutes);
        Assert.Equal(AppTheme.System, settings.Theme);
        Assert.Equal(AppBackdrop.Mica, settings.Backdrop);
        Assert.Equal(UserSettings.ChineseLanguage, settings.Language);
        Assert.Equal(UserSettings.DefaultDeviceName, settings.DeviceName);
        Assert.Empty(settings.Scenes);
    }

    [Fact]
    public void Partial_json_keeps_defaults_for_missing_fields()
    {
        var settings = JsonSerializer.Deserialize<UserSettings>(
            """{ "CloseToTray": true }""",
            SettingsStore.SerializerOptions)!;

        settings.Normalize();

        Assert.True(settings.CloseToTray);
        Assert.Equal("overview", settings.LastSection);
        Assert.Equal(UserSettings.DefaultDeviceName, settings.DeviceName);
        Assert.True(settings.AutoReconnect);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(-5)]
    [InlineData(60)]
    public void Unsupported_refresh_interval_falls_back_to_off(int minutes)
    {
        var settings = new UserSettings { AutoRefreshMinutes = minutes };

        settings.Normalize();

        Assert.Equal(0, settings.AutoRefreshMinutes);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(15)]
    public void Supported_refresh_interval_is_kept(int minutes)
    {
        var settings = new UserSettings { AutoRefreshMinutes = minutes };

        settings.Normalize();

        Assert.Equal(minutes, settings.AutoRefreshMinutes);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("en-US")]
    [InlineData(" EN-gb ")]
    public void English_language_is_normalized(string language)
    {
        Assert.Equal(UserSettings.EnglishLanguage, UserSettings.NormalizeLanguage(language));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("zh-CN")]
    [InlineData("fr-FR")]
    public void Other_languages_fall_back_to_chinese(string? language)
    {
        Assert.Equal(UserSettings.ChineseLanguage, UserSettings.NormalizeLanguage(language));
    }

    [Fact]
    public void Unknown_section_falls_back_to_overview()
    {
        var settings = new UserSettings { LastSection = "not-a-page" };

        settings.Normalize();

        Assert.Equal("overview", settings.LastSection);
    }

    [Fact]
    public void Blank_device_name_falls_back_to_default()
    {
        var settings = new UserSettings { DeviceName = "   ", DeviceAddress = "  " };

        settings.Normalize();

        Assert.Equal(UserSettings.DefaultDeviceName, settings.DeviceName);
        Assert.Null(settings.DeviceAddress);
    }

    [Fact]
    public void Half_written_window_size_is_discarded()
    {
        var settings = new UserSettings
        {
            WindowWidth = 1200,
            WindowHeight = null,
            WindowLeft = 100,
            WindowTop = 100,
        };

        settings.Normalize();

        Assert.Null(settings.WindowWidth);
        Assert.Null(settings.WindowHeight);
        Assert.Null(settings.WindowLeft);
        Assert.Null(settings.WindowTop);
    }

    [Fact]
    public void Window_size_is_clamped()
    {
        var settings = new UserSettings { WindowWidth = 10, WindowHeight = 99_999 };

        settings.Normalize();

        Assert.Equal(WindowPlacement.MinWidth, settings.WindowWidth);
        Assert.Equal(6000, settings.WindowHeight);
    }

    [Fact]
    public void Scenes_are_trimmed_deduplicated_and_bounded()
    {
        var ncasm = new TandemSceneNcAsm(
            TandemNcAsmMode.Nc,
            Enabled: true,
            Level: 40,
            VoiceFocus: false,
            NoiseAdaptive: false,
            TandemNoiseAdaptiveSensitivity.Standard);
        var settings = new UserSettings
        {
            Scenes =
            [
                new TandemSceneDefinition(" a ", "  Focus  ", ncasm, null, null),
                new TandemSceneDefinition("a", "duplicate", null, null, null),
                new TandemSceneDefinition("", "no id", null, null, null),
                new TandemSceneDefinition("b", "   ", null, null, null),
            ],
        };

        settings.Normalize();

        var scene = Assert.Single(settings.Scenes);
        Assert.Equal("a", scene.Id);
        Assert.Equal("Focus", scene.Name);
        Assert.Equal((byte)20, scene.NcAsm!.Level);
    }

    [Fact]
    public void Scenes_round_trip_through_json()
    {
        var original = new UserSettings
        {
            Theme = AppTheme.Dark,
            Backdrop = AppBackdrop.Acrylic,
            AutoRefreshMinutes = 5,
            Scenes =
            [
                new TandemSceneDefinition(
                    "night",
                    "夜间",
                    new TandemSceneNcAsm(
                        TandemNcAsmMode.Asm,
                        Enabled: true,
                        Level: 8,
                        VoiceFocus: true,
                        NoiseAdaptive: true,
                        TandemNoiseAdaptiveSensitivity.High),
                    new TandemSceneEq(TandemEqPresetId.Custom, [3, 4, 5, 6, 7, 8, 9, 10, 11, 12]),
                    new TandemSceneListening(TandemListeningMode.BackgroundMusic, TandemRoomSize.Small)),
            ],
        };

        var json = JsonSerializer.Serialize(original, SettingsStore.SerializerOptions);
        var restored = JsonSerializer.Deserialize<UserSettings>(json, SettingsStore.SerializerOptions)!;

        Assert.Equal(AppTheme.Dark, restored.Theme);
        Assert.Equal(AppBackdrop.Acrylic, restored.Backdrop);
        Assert.Equal(5, restored.AutoRefreshMinutes);
        var scene = Assert.Single(restored.Scenes);
        Assert.Equal("night", scene.Id);
        Assert.Equal("夜间", scene.Name);
        Assert.Equal(TandemNcAsmMode.Asm, scene.NcAsm!.Mode);
        Assert.Equal(8, (int)scene.NcAsm.Level);
        Assert.Equal(new[] { 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, scene.Eq!.CustomBands);
        Assert.Equal(TandemListeningMode.BackgroundMusic, scene.Listening!.Mode);
    }

    [Fact]
    public void Enum_strings_and_numbers_are_both_accepted()
    {
        var restored = JsonSerializer.Deserialize<UserSettings>(
            """{ "Theme": 2, "Backdrop": "Acrylic" }""",
            SettingsStore.SerializerOptions)!;

        Assert.Equal(AppTheme.Dark, restored.Theme);
        Assert.Equal(AppBackdrop.Acrylic, restored.Backdrop);
    }

    [Fact]
    public void Invalid_enum_number_falls_back_to_default()
    {
        var restored = JsonSerializer.Deserialize<UserSettings>(
            """{ "Theme": 42 }""",
            SettingsStore.SerializerOptions)!;

        restored.Normalize();

        Assert.Equal(AppTheme.System, restored.Theme);
    }
}
