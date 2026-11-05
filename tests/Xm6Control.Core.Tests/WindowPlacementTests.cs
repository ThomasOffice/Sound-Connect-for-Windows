namespace Xm6Control.Core.Tests;

public sealed class WindowPlacementTests
{
    private static readonly ScreenRect Primary = new(0, 0, 1920, 1040);

    [Fact]
    public void Missing_position_centers_on_primary()
    {
        var bounds = WindowPlacement.Resolve(null, null, 1000, 700, [Primary]);

        Assert.Equal(WindowPlacement.Center(Primary, 1000, 700), bounds);
    }

    [Fact]
    public void Saved_position_that_is_visible_is_kept()
    {
        var bounds = WindowPlacement.Resolve(120, 80, 900, 620, [Primary]);

        Assert.Equal(new WindowBounds(120, 80, 900, 620), bounds);
    }

    [Fact]
    public void Offscreen_position_falls_back_to_center()
    {
        var bounds = WindowPlacement.Resolve(5000, 4000, 900, 620, [Primary]);

        Assert.Equal(WindowPlacement.Center(Primary, 900, 620), bounds);
    }

    [Fact]
    public void Mostly_offscreen_position_falls_back_to_center()
    {
        // Only ~40px of the 900px window remains on screen.
        var bounds = WindowPlacement.Resolve(1880, 100, 900, 620, [Primary]);

        Assert.Equal(WindowPlacement.Center(Primary, 900, 620), bounds);
    }

    [Fact]
    public void Position_on_secondary_monitor_is_kept()
    {
        var secondary = new ScreenRect(1920, 0, 1280, 1024);

        var bounds = WindowPlacement.Resolve(2000, 100, 900, 620, [Primary, secondary]);

        Assert.Equal(new WindowBounds(2000, 100, 900, 620), bounds);
    }

    [Fact]
    public void Oversized_window_is_clamped_to_the_largest_work_area()
    {
        var bounds = WindowPlacement.Resolve(null, null, 50_000, 50_000, [Primary]);

        Assert.Equal(Primary.Width, bounds.Width);
        Assert.Equal(Primary.Height, bounds.Height);
    }

    [Fact]
    public void Tiny_saved_size_is_clamped_to_the_minimum()
    {
        var bounds = WindowPlacement.Resolve(100, 100, 100, 100, [Primary]);

        Assert.Equal(WindowPlacement.MinWidth, bounds.Width);
        Assert.Equal(WindowPlacement.MinHeight, bounds.Height);
    }

    [Fact]
    public void No_work_areas_still_returns_a_usable_size()
    {
        var bounds = WindowPlacement.Resolve(null, null, null, null, []);

        Assert.Equal(WindowPlacement.DefaultWidth, bounds.Width);
        Assert.Equal(WindowPlacement.DefaultHeight, bounds.Height);
    }

    [Fact]
    public void Center_never_goes_negative_for_a_smaller_work_area()
    {
        var small = new ScreenRect(0, 0, 400, 300);

        var bounds = WindowPlacement.Center(small, 900, 700);

        Assert.True(bounds.Left >= 0);
        Assert.True(bounds.Top >= 0);
    }
}
