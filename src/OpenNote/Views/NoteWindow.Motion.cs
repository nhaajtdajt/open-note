using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using OpenNote.Core;
using OpenNote.Native;

namespace OpenNote.Views;

/// <summary>
/// What happens while a note is dragged by its header:
///  - shake it and it fades away a little with every shake, vanishes, then comes back;
///  - throw it and it slides on, and bounces off the four edges of the screen;
///  - an archive bin slides in at the bottom-right, and a note dropped (or thrown) onto it is archived.
/// The note comes first: nothing here moves a note you only set down, a gentle drag looks exactly
/// as before, and "Note effects" in the tray menu turns it all off.
/// </summary>
internal partial class NoteWindow
{
    // How hard a release must be to count as a throw (DIPs per second), and the fastest a note may fly.
    const double ThrowMinSpeed = 1100, ThrowMaxSpeed = 5200;
    // The bin appears once the note has really been dragged, not for a click or a nudge.
    const double BinAfterDip = 28;
    const long BinAfterMs = 140;
    const long FlightMaxMs = 3500;

    static readonly Stopwatch Clock = Stopwatch.StartNew();
    static readonly IEasingFunction EaseOut = new QuarticEase { EasingMode = EasingMode.EaseOut };

    readonly List<MotionSample> _trail = [];
    ShakeDetector? _shake;
    ArchiveBinWindow? _bin;
    IReadOnlyList<PixelRect> _areas = [];
    PixelRect _binArea;
    PixelPoint _dragFrom;
    PixelRect _dragOrigin;
    long _dragStartMs, _fadeVersion;
    DispatcherTimer? _pump;
    bool _dragging, _vanishing, _binHot, _flying, _swallowing;

    // Flight and swallow state
    Flight _flight;
    int _flightWidth, _flightHeight;
    long _flightStartMs, _frameMs;
    PixelRect _swallowFrom;
    PixelPoint _swallowTo;

    bool EffectsOn => _manager.Settings.NoteEffects && SystemParameters.ClientAreaAnimation;
    double PixelsPerDip => VisualTreeHelper.GetDpi(this).DpiScaleX;
    bool Alive => PresentationSource.FromVisual(this) is not null && IsVisible;

    // ---------- Drag ----------

    void BeginDrag()
    {
        StopMotion();
        if (!EffectsOn || Win32.GetBounds(_hwnd) is not { } b)
            return;
        _dragging = true;
        _dragOrigin = b;
        _areas = Displays.WorkAreas();
        _dragFrom = Win32.CursorPosition();
        _dragStartMs = Clock.ElapsedMilliseconds;
        _shake = new ShakeDetector(90 * PixelsPerDip);
        StartPump();
        _trail.Clear();
        _trail.Add(new MotionSample(b.X, b.Y, _dragStartMs));
    }

    /// <summary>
    /// While the header is held, Windows runs its own move loop, and it only lets WPF update
    /// animations when something wakes it up. A held-still note would freeze the bin's lid and the
    /// fade half way, so a no-op tick keeps the loop (and the animations) moving.
    /// </summary>
    void StartPump()
    {
        _pump ??= new DispatcherTimer(DispatcherPriority.Send) { Interval = TimeSpan.FromMilliseconds(16) };
        _pump.Tick -= Nothing;
        _pump.Tick += Nothing;
        _pump.Start();
    }

    static void Nothing(object? sender, EventArgs e)
    {
    }

    /// <summary>Runs on every move while the header is held (DragMove is busy underneath).</summary>
    void OnDragMoved()
    {
        if (!_dragging || Win32.GetBounds(_hwnd) is not { } b)
            return;
        long now = Clock.ElapsedMilliseconds;
        _trail.Add(new MotionSample(b.X, b.Y, now));
        while (_trail.Count > 2 && now - _trail[0].Ms > 250)
            _trail.RemoveAt(0);

        TrackShake(b, now);
        TrackBin(now);
    }

    /// <summary>The header was let go. <paramref name="moved"/>: the note actually changed place.</summary>
    void EndDrag(bool moved)
    {
        if (!_dragging)
            return;
        _dragging = false;
        _pump?.Stop();
        _shake = null;
        if (!_vanishing && Opacity < 1)
            FadeTo(1, 160);

        var cursor = Win32.CursorPosition();
        if (_bin is not null && _binHot && _bin.Zone.Contains(cursor))
        {
            Swallow();
            return;
        }

        var (vx, vy) = Throw.ReleaseVelocity(_trail, Clock.ElapsedMilliseconds);
        double speed = Math.Sqrt(vx * vx + vy * vy);
        if (moved && speed >= ThrowMinSpeed * PixelsPerDip && StartFlight(vx, vy, speed))
            return;
        CloseBin();
    }

    /// <summary>Stops any flight or swallow (a new grab, closing, hiding) and puts the bin away.</summary>
    void StopMotion()
    {
        if (_swallowing)
            return; // the note is on its way into the bin: let that finish
        if (_flying)
        {
            CompositionTarget.Rendering -= OnFrame;
            _flying = false;
        }
        CloseBin();
    }

    /// <summary>The window is going away: nothing may keep running for it.</summary>
    void EndMotion()
    {
        CompositionTarget.Rendering -= OnFrame;
        _flying = _swallowing = _dragging = false;
        _pump?.Stop();
        CloseBin();
    }

    // ---------- Shake: fade a little with each shake, vanish, come back ----------

    void TrackShake(PixelRect b, long now)
    {
        if (_vanishing || _shake is null || !_shake.Feed(b.X, b.Y, now))
            return;
        if (_shake.Reversals >= ShakeDetector.Trigger)
        {
            _vanishing = true;
            FadeCycle(0, downMs: 150, holdMs: 520, upMs: 260); // gone for a moment, then back
            return;
        }
        // The warning: fainter with every shake. If the shaking stops it comes back by itself.
        FadeCycle(1 - 0.22 * _shake.Reversals, downMs: 90, holdMs: 500, upMs: 220);
    }

    /// <summary>
    /// Fades to <paramref name="level"/>, waits, and fades back to full. Animations, not timers:
    /// a timer does not tick while the header is held still inside Windows' move loop.
    /// </summary>
    void FadeCycle(double level, int downMs, int holdMs, int upMs)
    {
        long version = ++_fadeVersion;
        static KeyTime At(int ms) => KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(ms));
        var cycle = new DoubleAnimationUsingKeyFrames();
        cycle.KeyFrames.Add(new EasingDoubleKeyFrame(level, At(downMs), EaseOut));
        cycle.KeyFrames.Add(new LinearDoubleKeyFrame(level, At(downMs + holdMs)));
        cycle.KeyFrames.Add(new EasingDoubleKeyFrame(1, At(downMs + holdMs + upMs), EaseOut));
        cycle.Completed += (_, _) =>
        {
            if (version != _fadeVersion)
                return;
            BeginAnimation(OpacityProperty, null); // nothing keeps animating once fully back
            Opacity = 1;
            _vanishing = false;
            _shake?.Reset();
        };
        BeginAnimation(OpacityProperty, cycle);
    }

    void FadeTo(double to, int ms)
    {
        long version = ++_fadeVersion;
        var fade = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = EaseOut };
        fade.Completed += (_, _) =>
        {
            if (version != _fadeVersion || to < 1)
                return;
            BeginAnimation(OpacityProperty, null);
            Opacity = 1;
        };
        BeginAnimation(OpacityProperty, fade);
    }
    // ---------- Archive bin ----------

    void TrackBin(long now)
    {
        var cursor = Win32.CursorPosition();
        if (_bin is null)
        {
            double moved = Math.Max(Math.Abs(cursor.X - _dragFrom.X), Math.Abs(cursor.Y - _dragFrom.Y));
            if (moved < BinAfterDip * PixelsPerDip || now - _dragStartMs < BinAfterMs)
                return;
            OpenBin(cursor);
        }
        else if (_areas.Count > 1 && ScreenMath.AreaFor(cursor, _areas) is var area && area != _binArea)
        {
            PlaceBin(area); // the pointer went to another monitor: the bin follows
            _bin.SlideIn();
        }

        bool hot = _bin!.Zone.Contains(cursor);
        if (hot != _binHot)
        {
            _binHot = hot;
            _bin.SetHot(hot);
        }
    }

    void OpenBin(PixelPoint at)
    {
        _bin = new ArchiveBinWindow(animate: true) { Topmost = Topmost };
        _bin.Show();
        PlaceBin(ScreenMath.AreaFor(at, _areas.Count > 0 ? _areas : Displays.WorkAreas()));
        _bin.SlideIn();
    }

    void PlaceBin(PixelRect area)
    {
        _binArea = area;
        _bin!.PlaceOn(area, _hwnd);
    }

    void CloseBin()
    {
        _binHot = false;
        var bin = _bin;
        _bin = null;
        bin?.SlideOutAndClose();
    }

    // ---------- Throw: slide on, bounce off the four edges ----------

    bool StartFlight(double vx, double vy, double speed)
    {
        if (Win32.GetBounds(_hwnd) is not { } r)
            return false;
        var areas = _areas.Count > 0 ? _areas : Displays.WorkAreas();
        if (areas.Count == 0)
            return false;
        var area = ScreenMath.AreaFor(new PixelPoint(r.X + r.Width / 2, r.Y + r.Height / 2), areas);

        // The glass sits 12 DIPs inside the window (room for the shadow), so let the window go
        // that far past the edge: the glass itself is what touches the edge.
        double margin = 12 * PixelsPerDip;
        double minX = area.X - margin, minY = area.Y - margin;
        double maxX = area.Right - r.Width + margin, maxY = area.Bottom - r.Height + margin;
        if (maxX <= minX || maxY <= minY)
            return false;

        double cap = ThrowMaxSpeed * PixelsPerDip, scale = speed > cap ? cap / speed : 1;
        _flight = new Flight(r.X, r.Y, vx * scale, vy * scale, minX, minY, maxX, maxY);
        (_flightWidth, _flightHeight) = (r.Width, r.Height);
        _flightStartMs = _frameMs = Clock.ElapsedMilliseconds;
        _flying = true;
        if (_bin is null)
            OpenBin(new PixelPoint(r.X + r.Width / 2, r.Y + r.Height / 2));
        CompositionTarget.Rendering += OnFrame;
        return true;
    }

    void OnFrame(object? sender, EventArgs e)
    {
        long now = Clock.ElapsedMilliseconds;
        double seconds = Math.Min((now - _frameMs) / 1000.0, 0.05); // a stall must not make it jump
        if (seconds < 0.004)
            return; // Rendering can fire twice for one frame
        _frameMs = now;

        if (_swallowing)
        {
            SwallowFrame(now);
            return;
        }
        if (!Alive)
        {
            StopMotion();
            return;
        }

        bool moving = _flight.Step(seconds);
        var at = new PixelRect((int)Math.Round(_flight.X), (int)Math.Round(_flight.Y), _flightWidth, _flightHeight);
        Win32.SetBounds(_hwnd, at, moveOnly: true);

        // Flew over the bin: that is the point of throwing it there.
        if (_bin is { } bin && at.Contains(bin.Center))
        {
            Swallow();
            return;
        }
        if (!moving || now - _flightStartMs > FlightMaxMs)
            StopMotion();
    }

    // ---------- Swallow: the note shrinks into the bin and is archived ----------

    const long SwallowMs = 240;

    void Swallow()
    {
        if (_bin is null || Win32.GetBounds(_hwnd) is not { } from)
            return;
        if (_flying)
            CompositionTarget.Rendering -= OnFrame;
        _swallowing = true;
        _fadeVersion++;
        BeginAnimation(OpacityProperty, null); // a running fade would hold the opacity
        _swallowFrom = from;
        _swallowTo = _bin.Center;
        _frameMs = _flightStartMs = Clock.ElapsedMilliseconds;
        _bin.SetHot(true);

        var shrink = new ScaleTransform(1, 1);
        if (Content is FrameworkElement root)
        {
            root.RenderTransformOrigin = new Point(0.5, 0.5);
            root.RenderTransform = shrink;
        }
        _flying = true;
        CompositionTarget.Rendering += OnFrame;
    }

    void SwallowFrame(long now)
    {
        double t = Math.Min(1, (now - _flightStartMs) / (double)SwallowMs);
        double pull = t * t;                       // gathers speed as it is drawn in
        double cx = _swallowFrom.X + _swallowFrom.Width / 2.0, cy = _swallowFrom.Y + _swallowFrom.Height / 2.0;
        cx += (_swallowTo.X - cx) * pull;
        cy += (_swallowTo.Y - cy) * pull;
        Win32.SetBounds(_hwnd, new PixelRect((int)Math.Round(cx - _swallowFrom.Width / 2.0), (int)Math.Round(cy - _swallowFrom.Height / 2.0),
            _swallowFrom.Width, _swallowFrom.Height), moveOnly: true);
        if (Content is FrameworkElement { RenderTransform: ScaleTransform shrink })
            (shrink.ScaleX, shrink.ScaleY) = (1 - 0.85 * pull, 1 - 0.85 * pull);
        Opacity = 1 - pull;
        if (t < 1)
            return;

        CompositionTarget.Rendering -= OnFrame;
        _flying = _swallowing = false;
        var bin = _bin;
        _bin = null;
        bin?.Swallow();
        if (bin is not null)
        {
            var leave = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(380) };
            leave.Tick += (_, _) =>
            {
                leave.Stop();
                bin.SlideOutAndClose();
            };
            leave.Start();
        }

        // Archive it, and remember where it was before this drag, so restoring it from the list puts it back there.
        var note = Note;
        _manager.ArchiveNote(note);
        (note.X, note.Y) = (_dragOrigin.X, _dragOrigin.Y);
        _manager.NoteMoved();
    }
}
