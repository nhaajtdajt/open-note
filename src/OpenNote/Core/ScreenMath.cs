namespace OpenNote.Core;

public readonly record struct PixelPoint(int X, int Y);

public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;

    public bool Contains(PixelPoint p) => p.X >= X && p.X < Right && p.Y >= Y && p.Y < Bottom;

    public PixelRect Intersect(PixelRect o)
    {
        int x = Math.Max(X, o.X), y = Math.Max(Y, o.Y);
        int r = Math.Min(Right, o.Right), b = Math.Min(Bottom, o.Bottom);
        return r > x && b > y ? new PixelRect(x, y, r - x, b - y) : default;
    }

    public long Area => (long)Width * Height;
}

/// <summary>Keeps notes reachable when monitors are unplugged, rearranged or change resolution.</summary>
public static class ScreenMath
{
    /// <summary>Height of the strip at the top of a note used to drag it.</summary>
    public const int GripHeight = 24;

    /// <summary>
    /// Returns <paramref name="note"/> unchanged when enough of its drag strip is on some
    /// monitor to grab it; otherwise moves (and if needed shrinks) it onto the best monitor.
    /// </summary>
    public static PixelRect EnsureVisible(PixelRect note, IReadOnlyList<PixelRect> workAreas, int minVisible = 48)
    {
        if (workAreas.Count == 0)
            return note;

        var grip = new PixelRect(note.X, note.Y, note.Width, Math.Min(note.Height, GripHeight));
        foreach (var area in workAreas)
        {
            var hit = grip.Intersect(area);
            if (hit.Width >= Math.Min(minVisible, note.Width) && hit.Height >= Math.Min(GripHeight / 2, grip.Height))
                return note;
        }

        return ClampInto(note, BestArea(note, workAreas));
    }

    /// <summary>Moves the rectangle fully inside <paramref name="area"/>, shrinking it if it is larger.</summary>
    public static PixelRect ClampInto(PixelRect r, PixelRect area)
    {
        int w = Math.Min(r.Width, area.Width), h = Math.Min(r.Height, area.Height);
        int x = Math.Clamp(r.X, area.X, area.Right - w);
        int y = Math.Clamp(r.Y, area.Y, area.Bottom - h);
        return new PixelRect(x, y, w, h);
    }

    /// <summary>The work area containing the point, else the nearest one.</summary>
    public static PixelRect AreaFor(PixelPoint p, IReadOnlyList<PixelRect> workAreas)
    {
        foreach (var a in workAreas)
            if (a.Contains(p))
                return a;
        return workAreas.MinBy(a => DistanceSquared(a, p));
    }

    static PixelRect BestArea(PixelRect note, IReadOnlyList<PixelRect> workAreas)
    {
        var overlapping = workAreas.MaxBy(a => note.Intersect(a).Area);
        if (note.Intersect(overlapping).Area > 0)
            return overlapping;
        return AreaFor(new PixelPoint(note.X + note.Width / 2, note.Y + note.Height / 2), workAreas);
    }

    static long DistanceSquared(PixelRect a, PixelPoint p)
    {
        long dx = p.X < a.X ? a.X - p.X : p.X >= a.Right ? p.X - a.Right + 1 : 0;
        long dy = p.Y < a.Y ? a.Y - p.Y : p.Y >= a.Bottom ? p.Y - a.Bottom + 1 : 0;
        return dx * dx + dy * dy;
    }
}
