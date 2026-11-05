using Xm6Control.Core;

namespace Xm6Control.Core.Tests;

public sealed class SettingsStoreTests
{
    private static string NewTempDirectory()
    {
        var directory = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "xm6-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    [Fact]
    public void Missing_file_returns_defaults()
    {
        var directory = NewTempDirectory();
        try
        {
            var store = new SettingsStore(System.IO.Path.Combine(directory, "settings.json"));

            var settings = store.Load();

            Assert.Equal(new UserSettings().DeviceName, settings.DeviceName);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Save_then_load_round_trips()
    {
        var directory = NewTempDirectory();
        try
        {
            var path = System.IO.Path.Combine(directory, "settings.json");
            var store = new SettingsStore(path);
            var settings = new UserSettings
            {
                CloseToTray = true,
                StartMinimized = true,
                Theme = AppTheme.Dark,
                AutoRefreshMinutes = 15,
                DeviceName = "XM6 Office",
            };

            Assert.True(store.TrySave(settings, out var error), error);

            var loaded = store.Load();
            Assert.True(loaded.CloseToTray);
            Assert.True(loaded.StartMinimized);
            Assert.Equal(AppTheme.Dark, loaded.Theme);
            Assert.Equal(15, loaded.AutoRefreshMinutes);
            Assert.Equal("XM6 Office", loaded.DeviceName);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Save_replaces_an_existing_file_atomically()
    {
        var directory = NewTempDirectory();
        try
        {
            var path = System.IO.Path.Combine(directory, "settings.json");
            var store = new SettingsStore(path);

            Assert.True(store.TrySave(new UserSettings { DeviceName = "First" }, out _));
            Assert.True(store.TrySave(new UserSettings { DeviceName = "Second" }, out _));

            Assert.Equal("Second", store.Load().DeviceName);
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Corrupt_file_falls_back_and_is_kept_aside()
    {
        var directory = NewTempDirectory();
        try
        {
            var path = System.IO.Path.Combine(directory, "settings.json");
            File.WriteAllText(path, "{ not valid json");
            var store = new SettingsStore(path);

            var settings = store.Load();

            Assert.Equal(new UserSettings().DeviceName, settings.DeviceName);
            Assert.True(File.Exists(path + ".corrupt"));
            Assert.Equal("{ not valid json", File.ReadAllText(path + ".corrupt"));
            Assert.True(File.Exists(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Hand_edited_values_are_normalized_on_load()
    {
        var directory = NewTempDirectory();
        try
        {
            var path = System.IO.Path.Combine(directory, "settings.json");
            File.WriteAllText(
                path,
                """
                {
                  "AutoRefreshMinutes": 3,
                  "LastSection": "nope",
                  "Language": "en-GB",
                  "DeviceName": "  "
                }
                """);
            var store = new SettingsStore(path);

            var settings = store.Load();

            Assert.Equal(0, settings.AutoRefreshMinutes);
            Assert.Equal("overview", settings.LastSection);
            Assert.Equal(UserSettings.EnglishLanguage, settings.Language);
            Assert.Equal(UserSettings.DefaultDeviceName, settings.DeviceName);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
