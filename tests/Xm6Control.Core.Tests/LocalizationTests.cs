namespace Xm6Control.Core.Tests;

public sealed class LocalizationTests : IDisposable
{
    public LocalizationTests()
    {
        Localization.SetLanguage(UserSettings.ChineseLanguage);
    }

    public void Dispose()
    {
        Localization.SetLanguage(UserSettings.ChineseLanguage);
    }

    [Fact]
    public void Every_entry_has_both_languages()
    {
        foreach (var (key, entry) in Localization.Table)
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.Zh), $"Missing Chinese text for {key}");
            Assert.False(string.IsNullOrWhiteSpace(entry.En), $"Missing English text for {key}");
        }
    }

    [Fact]
    public void Format_placeholders_match_between_languages()
    {
        foreach (var (key, entry) in Localization.Table)
        {
            Assert.Equal(CountPlaceholders(entry.Zh), CountPlaceholders(entry.En));
        }
    }

    [Fact]
    public void Chinese_is_the_default()
    {
        Assert.False(Localization.IsEnglish);
        Assert.Equal("概览", Localization.Text("nav.overview"));
    }

    [Fact]
    public void Switching_to_english_changes_lookup()
    {
        Localization.SetLanguage(UserSettings.EnglishLanguage);

        Assert.True(Localization.IsEnglish);
        Assert.Equal("Overview", Localization.Text("nav.overview"));
    }

    [Fact]
    public void Switching_language_raises_changed_once()
    {
        var raised = 0;
        void Handler() => raised++;

        Localization.Changed += Handler;
        try
        {
            Localization.SetLanguage(UserSettings.EnglishLanguage);
            Localization.SetLanguage(UserSettings.EnglishLanguage);
            Localization.SetLanguage(UserSettings.ChineseLanguage);
        }
        finally
        {
            Localization.Changed -= Handler;
        }

        Assert.Equal(2, raised);
    }

    [Fact]
    public void Unknown_key_returns_the_key()
    {
        Assert.Equal("missing.key", Localization.Text("missing.key"));
    }

    [Fact]
    public void Format_substitutes_arguments()
    {
        Localization.SetLanguage(UserSettings.EnglishLanguage);

        Assert.Equal("Updated 12:30", Localization.Format("overview.updated", "12:30"));
    }

    private static int CountPlaceholders(string text)
    {
        var count = 0;
        for (var index = 0; index < text.Length - 1; index++)
        {
            if (text[index] == '{' && char.IsDigit(text[index + 1]))
            {
                count++;
            }
        }

        return count;
    }
}
