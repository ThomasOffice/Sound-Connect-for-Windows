namespace Xm6Control.Core.Tests;

public sealed class StartupCommandTests
{
    private const string Exe = @"C:\Program Files\XM6 Control\Xm6Control.App.exe";

    [Fact]
    public void Build_quotes_the_path()
    {
        Assert.Equal($"\"{Exe}\"", StartupCommand.Build(Exe));
    }

    [Fact]
    public void Build_does_not_double_quote()
    {
        Assert.Equal($"\"{Exe}\"", StartupCommand.Build($"\"{Exe}\""));
    }

    [Fact]
    public void ExtractPath_handles_quoted_and_bare_values()
    {
        Assert.Equal(Exe, StartupCommand.ExtractPath($"\"{Exe}\""));
        Assert.Equal(Exe, StartupCommand.ExtractPath(Exe));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ExtractPath_returns_null_for_empty_values(string? value)
    {
        Assert.Null(StartupCommand.ExtractPath(value));
    }

    [Fact]
    public void Matches_is_case_insensitive()
    {
        Assert.True(StartupCommand.Matches($"\"{Exe.ToUpperInvariant()}\"", Exe));
    }

    [Fact]
    public void Matches_rejects_a_different_executable()
    {
        Assert.False(StartupCommand.Matches(
            "\"C:\\Old\\Xm6Control.App.exe\"",
            Exe));
    }

    [Fact]
    public void Matches_rejects_a_missing_value()
    {
        Assert.False(StartupCommand.Matches(null, Exe));
    }
}
