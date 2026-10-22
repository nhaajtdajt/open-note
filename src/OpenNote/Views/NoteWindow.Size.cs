using System.Windows.Media;
using System.Windows.Threading;
using OpenNote.Core;
using OpenNote.Native;

namespace OpenNote.Views;

internal partial class NoteWindow
{
    // The size a new note starts with (the XAML Width/Height), in device-independent pixels.
    const double NormalWidthDip = 284, NormalHeightDip = 264;

    bool _resizingByToggle;
    DispatcherTimer? _renameTimer;

    /// <summary>
    /// A click on the header renames the note, but a double-click toggles the size. So a click
    /// waits one double-click time for a possible second click before it starts renaming.
    /// </summary>
    void ScheduleRename()
    {
        _renameTimer ??= new DispatcherTimer();
        _renameTimer.Interval = TimeSpan.FromMilliseconds(Win32.GetDoubleClickTime());
        _renameTimer.Tick -= RenameTimerTick;
        _renameTimer.Tick += RenameTimerTick;
        _renameTimer.Stop();
        _renameTimer.Start();
    }

    void RenameTimerTick(object? sender, EventArgs e)
    {
        CancelPendingRename();
        BeginTitleEdit();
    }

    void CancelPendingRename() => _renameTimer?.Stop();

    /// <summary>Shrinks a big note to the normal size and remembers its size; the next call restores that size.</summary>
    void ToggleSize()
    {
        if (Win32.GetBounds(_hwnd) is not { } bounds)
            return;
        double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var normal = new PixelSize((int)Math.Round(NormalWidthDip * scale), (int)Math.Round(NormalHeightDip * scale));
        PixelSize? restore = Note.RestoreWidth > 0 ? new PixelSize(Note.RestoreWidth, Note.RestoreHeight) : null;
        var (size, remember) = SizeToggle.Next(new PixelSize(bounds.Width, bounds.Height), restore, normal);

        // Keep the top-left corner, but never let a bigger size run off the screen.
        var wanted = bounds with { Width = size.Width, Height = size.Height };
        var area = ScreenMath.AreaFor(new PixelPoint(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2), Displays.WorkAreas());
        var target = ScreenMath.ClampInto(wanted, area);

        _resizingByToggle = true;
        try
        {
            Win32.SetBounds(_hwnd, target);
            Win32.SetBounds(_hwnd, target); // again, in case the monitor's scaling made WPF rescale the first time
        }
        finally
        {
            _resizingByToggle = false;
        }
        (Note.RestoreWidth, Note.RestoreHeight) = remember is { } r ? (r.Width, r.Height) : (0, 0);
        SaveBounds();
        _manager.NoteMoved();
    }
}
