using OpenNote.Core;

namespace OpenNote.Tests;

public class ScreenMathTests
{
    static readonly PixelRect Primary = new(0, 0, 1920, 1040);
    static readonly PixelRect LeftMonitor = new(-2560, -200, 2560, 1400);

    [Fact]
    public void NoteOnScreen_IsUnchanged()
    {
        var note = new PixelRect(100, 100, 260, 240);
        Assert.Equal(note, ScreenMath.EnsureVisible(note, [Primary]));
    }

    [Fact]
    public void NoteOnSecondMonitor_IsUnchangedWhileThatMonitorExists()
    {
        var note = new PixelRect(-1200, 300, 260, 240);
        Assert.Equal(note, ScreenMath.EnsureVisible(note, [Primary, LeftMonitor]));
    }

    [Fact]
    public void NoteOnUnpluggedMonitor_MovesOntoRemainingMonitor()
    {
        var note = new PixelRect(-1200, 300, 260, 240);
        var moved = ScreenMath.EnsureVisible(note, [Primary]);
        Assert.Equal(new PixelRect(0, 300, 260, 240), moved);
    }

    [Fact]
    public void NoteWithDragStripAboveScreen_IsPulledDown()
    {
        var note = new PixelRect(400, -150, 260, 240); // body visible, strip not grabbable
        var moved = ScreenMath.EnsureVisible(note, [Primary]);
        Assert.Equal(new PixelRect(400, 0, 260, 240), moved);
    }

    [Fact]
    public void MostlyOffRightEdge_ButStripGrabbable_IsLeftAlone()
    {
        var note = new PixelRect(1850, 500, 260, 240); // 70px of the strip still visible
        Assert.Equal(note, ScreenMath.EnsureVisible(note, [Primary]));
    }

    [Fact]
    public void NoteLargerThanScreen_IsShrunkToFit()
    {
        var moved = ScreenMath.EnsureVisible(new PixelRect(5000, 5000, 4000, 3000), [Primary]);
        Assert.Equal(Primary, moved);
    }

    [Fact]
    public void ResolutionDrop_PullsNoteBackInside()
    {
        var smaller = new PixelRect(0, 0, 1280, 680);
        var moved = ScreenMath.EnsureVisible(new PixelRect(1700, 900, 260, 240), [smaller]);
        Assert.Equal(new PixelRect(1020, 440, 260, 240), moved);
    }

    [Fact]
    public void AreaFor_PicksContainingMonitor_ElseNearest()
    {
        Assert.Equal(LeftMonitor, ScreenMath.AreaFor(new PixelPoint(-10, 10), [Primary, LeftMonitor]));
        Assert.Equal(Primary, ScreenMath.AreaFor(new PixelPoint(3000, 500), [Primary, LeftMonitor]));
    }
}
