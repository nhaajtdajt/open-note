using OpenNote.Core;

namespace OpenNote.Tests;

public class MotionTests
{
    static List<MotionSample> Path(params (double X, double Y, long Ms)[] points) =>
        points.Select(p => new MotionSample(p.X, p.Y, p.Ms)).ToList();

    // ---------- Throw ----------

    [Fact]
    public void ReleaseVelocity_IsTheSpeedOverTheLastMoments()
    {
        // 16 ms per step, 24 px per step = 1500 px/s to the right and 750 px/s down
        var samples = Path((0, 0, 0), (24, 12, 16), (48, 24, 32), (72, 36, 48), (96, 48, 64));
        var (vx, vy) = Throw.ReleaseVelocity(samples, releaseMs: 70);
        Assert.Equal(1500, vx, 1);
        Assert.Equal(750, vy, 1);
    }

    [Fact]
    public void ReleaseVelocity_IgnoresAnEarlierFastPart()
    {
        // fast at first, then slowed to a crawl right before letting go
        var samples = Path((0, 0, 0), (100, 0, 16), (200, 0, 32), (202, 0, 80), (203, 0, 100), (204, 0, 120));
        var (vx, _) = Throw.ReleaseVelocity(samples, releaseMs: 125);
        Assert.True(Math.Abs(vx) < 100);
    }

    [Fact]
    public void ReleaseVelocity_UsesTheLastTwoUpdates_WhenTheComputerWasBusyAndUpdatesAreSparse()
    {
        // only one update inside the last 90 ms: the one before it, 120 ms earlier, still tells the speed
        var samples = Path((0, 0, 0), (300, 0, 120), (420, 0, 240));
        var (vx, _) = Throw.ReleaseVelocity(samples, releaseMs: 250);
        Assert.Equal(1000, vx, 1);
        // but an update that old says nothing about how it was let go
        Assert.Equal((0, 0), Throw.ReleaseVelocity(Path((0, 0, 0), (300, 0, 400)), releaseMs: 410));
    }

    [Fact]
    public void ReleaseVelocity_IsZeroWhenTheNoteWasHeldStillBeforeLettingGo()
    {
        var samples = Path((0, 0, 0), (200, 0, 16), (400, 0, 32));
        Assert.Equal((0, 0), Throw.ReleaseVelocity(samples, releaseMs: 400));
        Assert.Equal((0, 0), Throw.ReleaseVelocity(Path((0, 0, 0)), releaseMs: 10));
    }

    // ---------- Shake ----------

    /// <summary>Back-and-forth strokes of <paramref name="width"/> px, one every <paramref name="strokeMs"/> ms.</summary>
    static (ShakeDetector Detector, int Triggered) Shake(int strokes, double width, int strokeMs, double minStroke = 90)
    {
        var d = new ShakeDetector(minStroke);
        int turns = 0;
        long ms = 0;
        for (int s = 0; s < strokes; s++)
        {
            for (int step = 1; step <= 8; step++)
            {
                ms += strokeMs / 8;
                double x = (s % 2 == 0 ? step : 8 - step) * width / 8;
                if (d.Feed(x, 0, ms))
                    turns++;
            }
        }
        return (d, turns);
    }

    [Fact]
    public void FastLongStrokes_AreAShake()
    {
        var (d, _) = Shake(strokes: 7, width: 140, strokeMs: 160);
        Assert.True(d.Reversals >= ShakeDetector.Trigger);
    }

    [Fact]
    public void SlowStrokes_AreNotAShake()
    {
        var (d, _) = Shake(strokes: 10, width: 140, strokeMs: 900);
        Assert.True(d.Reversals < ShakeDetector.Trigger);
    }

    [Fact]
    public void ShortWiggles_AreNotAShake()
    {
        var (d, _) = Shake(strokes: 12, width: 40, strokeMs: 120);
        Assert.Equal(0, d.Reversals);
    }

    [Fact]
    public void OneLongMovePlusACorrection_IsNotAShake()
    {
        var d = new ShakeDetector(90);
        long ms = 0;
        for (int x = 0; x <= 600; x += 25) d.Feed(x, 0, ms += 16);   // across the screen
        for (int x = 600; x >= 520; x -= 10) d.Feed(x, 0, ms += 16); // back a little
        for (int x = 520; x <= 560; x += 10) d.Feed(x, 0, ms += 16); // settle
        Assert.True(d.Reversals < ShakeDetector.Trigger);
    }

    [Fact]
    public void StrokesFarApartInTime_DoNotAddUp()
    {
        var d = new ShakeDetector(90);
        long ms = 0;
        for (int round = 0; round < 6; round++)
        {
            ms += 2000; // a pause longer than the shake window between each pair of strokes
            for (int x = 0; x <= 140; x += 20) d.Feed(x, 0, ms += 20);
            for (int x = 140; x >= 0; x -= 20) d.Feed(x, 0, ms += 20);
        }
        Assert.True(d.Reversals < ShakeDetector.Trigger);
    }

    [Fact]
    public void ADiagonalTurn_CountsOnce()
    {
        var d = new ShakeDetector(90);
        long ms = 0;
        for (int i = 0; i <= 8; i++) d.Feed(i * 20, i * 20, ms += 20);   // down-right
        for (int i = 8; i >= 0; i--) d.Feed(i * 20, i * 20, ms += 20);   // back up-left
        Assert.Equal(1, d.Reversals);
    }

    // ---------- Flight ----------

    static int Run(ref Flight f, int maxFrames = 1000)
    {
        int frames = 0;
        while (f.Step(1 / 60.0) && frames++ < maxFrames) { }
        return frames;
    }

    [Fact]
    public void AThrownNote_SlowsDownAndComesToRest()
    {
        var f = new Flight(500, 300, 900, 0, 0, 0, 5000, 3000);
        int frames = Run(ref f);
        Assert.True(frames < 600, "should stop within a few seconds");
        Assert.True(f.X > 500 && f.X < 5000);
        Assert.Equal(0, f.Bounces);
    }

    [Fact]
    public void ANoteThrownHard_BouncesOffTheEdgesAndStaysInside()
    {
        var f = new Flight(400, 300, 3200, -2600, 0, 0, 900, 600);
        for (int i = 0; i < 600; i++)
        {
            bool moving = f.Step(1 / 60.0);
            Assert.InRange(f.X, 0, 900);
            Assert.InRange(f.Y, 0, 600);
            if (!moving) break;
        }
        Assert.True(f.Bounces >= 2);
    }

    [Fact]
    public void Bouncing_TurnsTheSpeedAroundAndLosesSome()
    {
        var f = new Flight(895, 300, 1200, 0, 0, 0, 900, 600); // 19 px in this frame: 14 px past the wall
        Assert.True(f.Step(1 / 60.0));
        Assert.Equal(1, f.Bounces);
        Assert.True(f.Vx < 0);
        Assert.True(Math.Abs(f.Vx) < 1200 * Flight.Bounce);
        Assert.InRange(f.X, 880, 900); // reflected back inside, not left past the wall
    }

    [Fact]
    public void ALargeFrameGap_DoesNotThrowTheNoteThroughAWall()
    {
        var f = new Flight(100, 100, 5000, 5000, 0, 0, 300, 300);
        f.Step(0.05);
        Assert.InRange(f.X, 0, 300);
        Assert.InRange(f.Y, 0, 300);
    }

    [Fact]
    public void ABoxSmallerThanTheNote_JustPinsItInPlace()
    {
        var f = new Flight(50, 50, 800, 800, 10, 10, 5, 5); // inverted: no room to travel
        f.Step(1 / 60.0);
        Assert.Equal(10, f.X);
        Assert.Equal(10, f.Y);
    }
}
