using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using OpenNote.Core;
using OpenNote.Native;

namespace OpenNote.Views;

/// <summary>
/// The archive bin in the bottom-right corner. It exists only while a note is being dragged or
/// thrown: it jumps in from the right edge of the screen, heaps up its paper when a note is over it,
/// and slides away again. Dropping a note on it archives the note.
/// </summary>
internal partial class ArchiveBinWindow : Window
{
    // Layout in DIPs (see the XAML): the bin is 112 x 124, 14 from the right and 12 from the bottom.
    const double BinWidth = 112, BinHeight = 124, RightGap = 14, BottomGap = 12, Reach = 40;

    // Strong ease-out: starts fast, settles gently.
    static readonly IEasingFunction Out = new QuarticEase { EasingMode = EasingMode.EaseOut };
    static readonly IEasingFunction In = new QuadraticEase { EasingMode = EasingMode.EaseIn };
    // "Jumps out": slightly past its place, then back.
    static readonly IEasingFunction Hop = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 };

    readonly bool _animate;
    bool _closing;

    /// <param name="animate">False when effects are off: the bin just appears and disappears.</param>
    public ArchiveBinWindow(bool animate)
    {
        InitializeComponent();
        _animate = animate;
    }

    nint _hwnd;

    /// <summary>Everything at and beyond the bin, towards the screen corner (the corner is easy to hit).</summary>
    public PixelRect Zone { get; private set; }

    /// <summary>The middle of the bin in screen pixels: where a note gets drawn into.</summary>
    public PixelPoint Center { get; private set; }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        Win32.MakeOverlayWindow(_hwnd);
    }

    /// <summary>
    /// Puts the bin flush in the bottom-right corner of <paramref name="area"/>, right behind
    /// the note being dragged so the note always stays on top of it.
    /// </summary>
    public void PlaceOn(PixelRect area, nint behind)
    {
        // Move into the monitor first: WPF resizes the window for that monitor's scaling.
        Win32.MoveBehind(_hwnd, behind, area.Right - 200, area.Bottom - 200);
        if (Win32.GetBounds(_hwnd) is not { } size)
            return;
        int x = area.Right - size.Width, y = area.Bottom - size.Height;
        Win32.MoveBehind(_hwnd, behind, x, y);

        double scale = size.Width / Width; // pixels per DIP on this monitor
        double binLeft = Width - RightGap - BinWidth, binTop = Height - BottomGap - BinHeight;
        // Open towards the screen corner (and the taskbar below): the corner is easy to hit.
        int zoneX = x + (int)((binLeft - Reach) * scale), zoneY = y + (int)((binTop - Reach) * scale);
        Zone = new PixelRect(zoneX, zoneY, area.Right + 400 - zoneX, area.Bottom + 400 - zoneY);
        Center = new PixelPoint(
            x + (int)((Width - RightGap - BinWidth / 2) * scale), y + (int)((Height - BottomGap - BinHeight / 2) * scale));
    }

    /// <summary>Jumps in from the right edge of the screen.</summary>
    public void SlideIn()
    {
        if (!_animate)
        {
            Jump(Slide, TranslateTransform.XProperty, 0);
            Jump(Stage, OpacityProperty, 1);
            return;
        }
        Run(Slide, TranslateTransform.XProperty, 0, 340, Hop, from: Width + 20);
        Run(Stage, OpacityProperty, 1, 120, Out, from: 0);
    }

    /// <summary>A note is over the bin (or not): the paper rises, the bin grows a little and glows.</summary>
    public void SetHot(bool hot)
    {
        int ms = hot ? 170 : 140;
        Run(PaperLift, TranslateTransform.YProperty, hot ? -9 : 0, ms, Out);
        Run(BinScale, ScaleTransform.ScaleXProperty, hot ? 1.08 : 1, ms, Out);
        Run(BinScale, ScaleTransform.ScaleYProperty, hot ? 1.08 : 1, ms, Out);
        Run(Warm, OpacityProperty, hot ? 0.32 : 0, ms, Out);
        Run(Glow, OpacityProperty, hot ? 1 : 0, ms, Out);
        Run(Label, OpacityProperty, hot ? 1 : 0, ms, Out);
        Run(LabelShift, TranslateTransform.YProperty, hot ? 0 : 6, ms, Out);
    }

    /// <summary>The note is in: the paper settles back down and the bin gives a little squash.</summary>
    public void Swallow()
    {
        Run(Glow, OpacityProperty, 0, 260, Out);
        Run(Label, OpacityProperty, 0, 200, Out);
        Run(Warm, OpacityProperty, 0, 260, Out);
        if (!_animate)
        {
            Jump(PaperLift, TranslateTransform.YProperty, 0);
            return;
        }
        var settle = new DoubleAnimationUsingKeyFrames();
        settle.KeyFrames.Add(new LinearDoubleKeyFrame(5, TimeSpan.FromMilliseconds(80)));
        settle.KeyFrames.Add(new EasingDoubleKeyFrame(0, TimeSpan.FromMilliseconds(360), new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.9 }));
        PaperLift.BeginAnimation(TranslateTransform.YProperty, settle);
        foreach (var property in new[] { ScaleTransform.ScaleXProperty, ScaleTransform.ScaleYProperty })
        {
            var squash = new DoubleAnimationUsingKeyFrames();
            squash.KeyFrames.Add(new LinearDoubleKeyFrame(0.92, TimeSpan.FromMilliseconds(80)));
            squash.KeyFrames.Add(new EasingDoubleKeyFrame(1, TimeSpan.FromMilliseconds(320), new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.8 }));
            BinScale.BeginAnimation(property, squash);
        }
    }

    /// <summary>Slides back out to the right and closes. Leaving is quicker than arriving.</summary>
    public void SlideOutAndClose()
    {
        if (_closing)
            return;
        _closing = true;
        if (!_animate || !IsVisible)
        {
            Close();
            return;
        }
        Run(Stage, OpacityProperty, 0, 170, In);
        var slide = new DoubleAnimation(Width + 20, TimeSpan.FromMilliseconds(170)) { EasingFunction = In };
        slide.Completed += (_, _) => Close();
        Slide.BeginAnimation(TranslateTransform.XProperty, slide);
    }

    // ---------- Animation helpers ----------

    /// <summary>Animates one property to <paramref name="to"/>; when effects are off it just jumps there.</summary>
    void Run(DependencyObject target, DependencyProperty property, double to, int ms, IEasingFunction ease, double? from = null)
    {
        if (!_animate)
        {
            Jump(target, property, to);
            return;
        }
        var animation = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = ease };
        if (from is { } start)
            animation.From = start;
        ((IAnimatable)target).BeginAnimation(property, animation);
    }

    static void Jump(DependencyObject target, DependencyProperty property, double value)
    {
        ((IAnimatable)target).BeginAnimation(property, null);
        target.SetValue(property, value);
    }
}
