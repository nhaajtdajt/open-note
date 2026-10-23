namespace OpenNote.Core;

/// <summary>Where a dragged note was, and when (milliseconds on a steady clock).</summary>
public readonly record struct MotionSample(double X, double Y, long Ms);

/// <summary>How fast a note was moving when the mouse button was let go.</summary>
public static class Throw
{
    const long LookBackMs = 90;   // speed is measured over the last moments of the drag only
    const long MaxIdleMs = 110;   // holding still before letting go means "put it down", not "throw it"
    const long SparseMs = 180;    // window updates can be sparse while the PC is busy: still use the last two

    /// <returns>Velocity in pixels per second, or (0, 0) when the note was set down gently.</returns>
    public static (double Vx, double Vy) ReleaseVelocity(IReadOnlyList<MotionSample> samples, long releaseMs)
    {
        if (samples.Count < 2)
            return (0, 0);
        var last = samples[^1];
        if (releaseMs - last.Ms > MaxIdleMs)
            return (0, 0);

        int first = samples.Count - 1;
        while (first > 0 && last.Ms - samples[first - 1].Ms <= LookBackMs)
            first--;
        if (first == samples.Count - 1 && last.Ms - samples[first - 1].Ms <= SparseMs)
            first--;
        var from = samples[first];
        double seconds = (last.Ms - from.Ms) / 1000.0;
        return seconds < 0.015 ? (0, 0) : ((last.X - from.X) / seconds, (last.Y - from.Y) / seconds);
    }
}

/// <summary>
/// Recognises a note being shaken: several quick, long strokes back and forth. Ordinary
/// repositioning (one long move, a small correction) never counts.
/// </summary>
public sealed class ShakeDetector(double minStroke)
{
    /// <summary>Direction changes that make a shake.</summary>
    public const int Trigger = 4;

    const long MaxStrokeMs = 320;     // each stroke must be quick…
    const long WindowMs = 1500;       // …and the strokes must follow each other
    const long SameShakeMs = 120;     // a diagonal shake turns both axes at once: one turn, not two
    const double TurnDistance = 14;   // moving back this far means the direction changed

    readonly Stroke _x = new(), _y = new();
    readonly List<long> _turns = [];

    /// <summary>Direction changes in the last moments.</summary>
    public int Reversals => _turns.Count;

    public void Reset() => _turns.Clear();

    /// <returns>True when this position completed another direction change.</returns>
    public bool Feed(double x, double y, long ms)
    {
        _turns.RemoveAll(t => ms - t > WindowMs);
        bool turned = _x.Feed(x, ms, minStroke) | _y.Feed(y, ms, minStroke);
        if (!turned || (_turns.Count > 0 && ms - _turns[^1] < SameShakeMs))
            return false;
        _turns.Add(ms);
        return true;
    }

    /// <summary>Follows one axis: its current stroke and when the direction last flipped.</summary>
    sealed class Stroke
    {
        bool _started;
        int _direction;
        double _start, _far;
        long _startMs, _farMs;

        public bool Feed(double v, long ms, double minStroke)
        {
            if (!_started)
            {
                (_started, _start, _far, _startMs, _farMs) = (true, v, v, ms, ms);
                return false;
            }
            if (_direction == 0)
            {
                if (Math.Abs(v - _start) >= TurnDistance)
                    (_direction, _far, _farMs) = (v > _start ? 1 : -1, v, ms);
                return false;
            }
            if ((v - _far) * _direction >= 0)
            {
                if (v != _far)
                    (_far, _farMs) = (v, ms); // still going the same way
                return false;
            }
            if ((_far - v) * _direction < TurnDistance)
                return false; // a small wobble

            bool strong = Math.Abs(_far - _start) >= minStroke && _farMs - _startMs <= MaxStrokeMs;
            (_start, _startMs, _far, _farMs, _direction) = (_far, _farMs, v, ms, -_direction);
            return strong;
        }
    }
}

/// <summary>
/// A note sliding after being thrown: it slows down, and bounces off the four edges of its
/// area. Positions are the window's top-left corner, in pixels.
/// </summary>
public struct Flight
{
    /// <summary>Speed fades by this factor of e per second (a higher number stops sooner).</summary>
    public const double Drag = 3.0;
    /// <summary>Share of its speed a note keeps after hitting an edge.</summary>
    public const double Bounce = 0.72;
    public const double RestSpeed = 40;

    readonly double _minX, _minY, _maxX, _maxY;

    public double X, Y, Vx, Vy;
    public int Bounces;

    /// <summary>The window's top-left corner may travel inside this box.</summary>
    public Flight(double x, double y, double vx, double vy, double minX, double minY, double maxX, double maxY)
    {
        (_minX, _minY, _maxX, _maxY) = (minX, minY, Math.Max(minX, maxX), Math.Max(minY, maxY));
        (X, Y, Vx, Vy, Bounces) = (Math.Clamp(x, _minX, _maxX), Math.Clamp(y, _minY, _maxY), vx, vy, 0);
    }

    /// <summary>Moves on by <paramref name="seconds"/>. Returns false once it has come to rest.</summary>
    public bool Step(double seconds)
    {
        double slow = Math.Exp(-Drag * seconds);
        Vx *= slow;
        Vy *= slow;
        (X, Vx) = Reflect(X + Vx * seconds, Vx, _minX, _maxX);
        (Y, Vy) = Reflect(Y + Vy * seconds, Vy, _minY, _maxY);
        return Math.Sqrt(Vx * Vx + Vy * Vy) >= RestSpeed;
    }

    (double Position, double Velocity) Reflect(double p, double v, double min, double max)
    {
        if (p < min)
        {
            Bounces++;
            return (Math.Min(max, min + (min - p)), -v * Bounce);
        }
        if (p > max)
        {
            Bounces++;
            return (Math.Max(min, max - (p - max)), -v * Bounce);
        }
        return (p, v);
    }
}
