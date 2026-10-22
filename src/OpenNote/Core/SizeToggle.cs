namespace OpenNote.Core;

public readonly record struct PixelSize(int Width, int Height);

/// <summary>
/// Double-clicking a note's header toggles its size: a big note shrinks to the normal size (and
/// remembers how big it was); the next double-click brings back the size it had just before.
/// </summary>
public static class SizeToggle
{
    /// <param name="current">The note's size now.</param>
    /// <param name="restore">The size to go back to, if the note was shrunk by a double-click.</param>
    /// <param name="normal">The default note size.</param>
    /// <returns>The size to apply and the size to remember (null = nothing to restore).</returns>
    public static (PixelSize Size, PixelSize? Restore) Next(PixelSize current, PixelSize? restore, PixelSize normal, int tolerance = 4)
    {
        if (restore is { } back)
            return (back, null);                          // second double-click: back to the last size
        if (Math.Abs(current.Width - normal.Width) <= tolerance && Math.Abs(current.Height - normal.Height) <= tolerance)
            return (current, null);                       // already normal: nothing to shrink or remember
        return (normal, current);                         // shrink to normal and remember the size it had
    }
}
