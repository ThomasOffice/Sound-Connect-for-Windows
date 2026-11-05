using System.Text.Json;
using System.Text.Json.Serialization;

namespace Xm6Control.Core;

/// <summary>
/// Loads and saves the local preferences file. Reads never throw: a missing,
/// unreadable, or malformed file falls back to defaults and the original bytes
/// are kept aside as <c>settings.json.corrupt</c> instead of being overwritten.
/// </summary>
public sealed class SettingsStore
{
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: true) },
    };

    public SettingsStore(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Settings path must not be empty.", nameof(path));
        }

        Path = path;
    }

    public string Path { get; }

    public UserSettings Load()
    {
        try
        {
            if (!File.Exists(Path))
            {
                return new UserSettings();
            }

            var json = File.ReadAllText(Path);
            var settings = JsonSerializer.Deserialize<UserSettings>(json, SerializerOptions);
            if (settings is null)
            {
                Quarantine();
                return new UserSettings();
            }

            settings.Normalize();
            return settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Quarantine();
            return new UserSettings();
        }
    }

    public bool TrySave(UserSettings settings, out string? error)
    {
        error = null;
        try
        {
            var directory = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporary = Path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings, SerializerOptions));
            File.Move(temporary, Path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            error = ex.Message;
            return false;
        }
    }

    private void Quarantine()
    {
        try
        {
            if (File.Exists(Path))
            {
                File.Copy(Path, Path + ".corrupt", overwrite: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Keeping the original is best effort; never replace defaults with it.
        }
    }
}
