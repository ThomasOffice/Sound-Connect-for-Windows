namespace Xm6Control.Core;

/// <summary>A monitor work area in physical pixels.</summary>
public readonly record struct ScreenRect(int Left, int Top, int Width, int Height)
{
    public int Right => Left + Width;

    public int Bottom => Top + Height;

    public long Area => (long)Math.Max(Width, 0) * Math.Max(Height, 0);

    public long IntersectionArea(ScreenRect other)
    {
        var left = Math.Max(Left, other.Left);
        var top = Math.Max(Top, other.Top);
        var right = Math.Min(Right, other.Right);
        var bottom = Math.Min(Bottom, other.Bottom);
        if (right <= left || bottom <= top)
        {
            return 0;
        }

        return (long)(right - left) * (bottom - top);
    }
}

public readonly record struct WindowBounds(int Left, int Top, int Width, int Height);

public static class WindowPlacement
{
    public const int MinWidth = 720;
    public const int MinHeight = 520;
    public const int DefaultWidth = 920;
    public const int DefaultHeight = 600;

    private const double MinimumVisibleFraction = 0.25;

    /// <summary>
    /// Resolves the window placement to restore. Falls back to centering on the
    /// first work area (the primary display) when the saved position is missing,
    /// malformed, or mostly off-screen after a monitor change.
    /// </summary>
    public static WindowBounds Resolve(
        int? left,
        int? top,
        int? width,
        int? height,
        IReadOnlyList<ScreenRect> workAreas)
    {
        if (workAreas.Count == 0)
        {
            var fallbackWidth = Math.Max(width ?? DefaultWidth, MinWidth);
            var fallbackHeight = Math.Max(height ?? DefaultHeight, MinHeight);
            return new WindowBounds(0, 0, fallbackWidth, fallbackHeight);
        }

        var widest = workAreas.Max(area => area.Width);
        var tallest = workAreas.Max(area => area.Height);
        var resolvedWidth = Math.Clamp(width ?? DefaultWidth, MinWidth, Math.Max(widest, MinWidth));
        var resolvedHeight = Math.Clamp(height ?? DefaultHeight, MinHeight, Math.Max(tallest, MinHeight));

        if (left is null || top is null)
        {
            return Center(workAreas[0], resolvedWidth, resolvedHeight);
        }

        var candidate = new ScreenRect(left.Value, top.Value, resolvedWidth, resolvedHeight);
        var visible = 0L;
        foreach (var area in workAreas)
        {
            visible += candidate.IntersectionArea(area);
        }

        if (candidate.Area == 0 || visible < candidate.Area * MinimumVisibleFraction)
        {
            return Center(workAreas[0], resolvedWidth, resolvedHeight);
        }

        return new WindowBounds(candidate.Left, candidate.Top, candidate.Width, candidate.Height);
    }

    public static WindowBounds Center(ScreenRect workArea, int width, int height)
    {
        var w = Math.Clamp(width, MinWidth, Math.Max(workArea.Width, MinWidth));
        var h = Math.Clamp(height, MinHeight, Math.Max(workArea.Height, MinHeight));
        return new WindowBounds(
            workArea.Left + Math.Max(0, (workArea.Width - w) / 2),
            workArea.Top + Math.Max(0, (workArea.Height - h) / 2),
            w,
            h);
    }
}
