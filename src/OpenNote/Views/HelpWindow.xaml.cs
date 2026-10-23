using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using OpenNote.Core;
using OpenNote.Native;

namespace OpenNote.Views;

/// <summary>
/// The guide: a short card in the middle of the screen with the shortcuts (the global ones as
/// they are set in settings.json). Built when asked for and dropped when closed.
/// </summary>
internal partial class HelpWindow : Window
{
    static readonly FontFamily KeyFont = new("Consolas");
    static readonly FontFamily TextFont = new("Segoe UI Variable Text, Segoe UI");
    static readonly Brush KeyBrush = Frozen(Color.FromRgb(0x5A, 0x24, 0x1E));
    static readonly Brush TextBrush = Frozen(Color.FromRgb(0x3B, 0x2A, 0x1E));

    bool _closing;

    public HelpWindow(AppSettings s)
    {
        InitializeComponent();
        AddRows(Anywhere,
        [
            (s.NewNoteHotkey, "New note"),
            (s.ToggleAllHotkey, "Hide / show all notes"),
            (s.NotesListHotkey, "All notes and archive"),
            (s.PickNotesHotkey, "Choose notes to keep"),
            (s.SavedNotesHotkey, "Show the kept notes"),
            (s.ApiKeysHotkey, "Gemini keys for AI"),
            (s.HelpHotkey, "This guide"),
            (s.QuitHotkey, "Quit Open Note"),
        ]);
        AddRows(InNote,
        [
            ("Ctrl+B / I", "Bold / italic"),
            ("/-   />", "Bullet / checkbox"),
            ("Ctrl+G", "Check or translate (AI)"),
            ("Ctrl+R", "Read aloud"),
            ("Ctrl+Shift+S", "Star: important"),
            ("Ctrl+N", "New note next to it"),
            ("Ctrl+W", "Close the note"),
            ("Ctrl+D", "Archive the note"),
        ]);

        // Closing deactivates the window too, so guard against closing twice.
        Closing += (_, _) => _closing = true;
        Deactivated += (_, _) => CloseOnce();
        MouseLeftButtonUp += (_, _) => CloseOnce();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                CloseOnce();
        };
        Loaded += (_, _) => Appear();
    }

    void CloseOnce()
    {
        if (!_closing)
            Close();
    }

    static void AddRows(Panel panel, (string Keys, string What)[] rows)
    {
        foreach (var (keys, what) in rows)
        {
            if (string.IsNullOrWhiteSpace(keys))
                continue; // a shortcut turned off in settings.json
            var row = new Grid { Margin = new Thickness(0, 1.5, 0, 1.5) };
            // The key column is as wide as the longest shortcut in this list, the same in every row.
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "Keys", MinWidth = 96 });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.Children.Add(new TextBlock { Text = keys, FontFamily = KeyFont, FontSize = 12.5, FontWeight = FontWeights.Bold, Foreground = KeyBrush, Margin = new Thickness(0, 0, 10, 0) });
            var text = new TextBlock { Text = what, FontFamily = TextFont, FontSize = 13, Foreground = TextBrush, TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetColumn(text, 1);
            row.Children.Add(text);
            panel.Children.Add(row);
        }
    }

    /// <summary>In the middle of the screen the mouse is on, out of Alt+Tab.</summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        Win32.MakeNoteWindow(hwnd);
        var area = ScreenMath.AreaFor(Win32.CursorPosition(), Displays.WorkAreas());
        // Move onto that monitor first, so WPF sizes the card for its scaling, then centre it.
        Win32.SetBounds(hwnd, new PixelRect(area.X + 8, area.Y + 8, 0, 0), moveOnly: true);
        if (Win32.GetBounds(hwnd) is { } size)
            Win32.SetBounds(hwnd, size with { X = area.X + (area.Width - size.Width) / 2, Y = area.Y + (area.Height - size.Height) / 2 }, moveOnly: true);
    }

    /// <summary>A short fade and settle; nothing at all when Windows animations are off.</summary>
    void Appear()
    {
        if (!SystemParameters.ClientAreaAnimation)
            return;
        var ease = new QuarticEase { EasingMode = EasingMode.EaseOut };
        var time = TimeSpan.FromMilliseconds(180);
        Root.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, time) { EasingFunction = ease });
        var settle = new DoubleAnimation(0.96, 1, time) { EasingFunction = ease };
        Pop.BeginAnimation(ScaleTransform.ScaleXProperty, settle);
        Pop.BeginAnimation(ScaleTransform.ScaleYProperty, settle);
    }

    static Brush Frozen(Color c)
    {
        var brush = new SolidColorBrush(c);
        brush.Freeze();
        return brush;
    }
}
