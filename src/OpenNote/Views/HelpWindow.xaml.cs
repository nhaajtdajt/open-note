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

    readonly AppSettings _settings;
    readonly Action<Core.Language> _setLanguage;
    bool _closing, _choosing;

    /// <param name="setLanguage">Changes the app's display language (and remembers it).</param>
    public HelpWindow(AppSettings s, Action<Core.Language> setLanguage)
    {
        InitializeComponent();
        _settings = s;
        _setLanguage = setLanguage;
        Fill();

        // Closing deactivates the window too, so guard against closing twice.
        Closing += (_, _) => _closing = true;
        Deactivated += (_, _) =>
        {
            if (!_choosing) // the language list is open
                CloseOnce();
        };
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

    /// <summary>The language pill: a list of the seven languages; picking one changes the whole app.</summary>
    void LanguageButton_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = LanguageButton, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        foreach (var language in Core.Language.All)
        {
            var item = new MenuItem { Header = language.NativeName, IsChecked = language == Ui.Current };
            item.Click += (_, _) =>
            {
                _setLanguage(language);
                Fill();
            };
            menu.Items.Add(item);
        }
        _choosing = true;
        menu.Closed += (_, _) =>
        {
            _choosing = false;
            Activate(); // back to the card, so clicking elsewhere still closes it
        };
        menu.IsOpen = true;
    }

    /// <summary>Writes the card in the display language (the shortcuts as set in settings.json).</summary>
    void Fill()
    {
        var s = _settings;
        Subtitle.Text = Ui.T("your sticky notes, and the keys that move them");
        LanguageText.Text = Ui.Current.NativeName + "  ▾";
        AnywhereHeading.Text = Ui.T("Anywhere");
        InNoteHeading.Text = Ui.T("In a note");
        // Word joiners keep the shortcut on one line.
        Tips.Text = Ui.T("Drag a note by its top bar · throw it on the bin to archive · ☆ marks it important · with {0} on, notes turn see-through while you use other apps; click one to bring them back.",
            s.FadeHotkey.Replace("+", "⁠+⁠"));
        CloseHint.Text = Ui.T("Click anywhere or press Esc to close");

        Anywhere.Children.Clear();
        InNote.Children.Clear();
        AddRows(Anywhere,
        [
            (s.NewNoteHotkey, Ui.T("New note")),
            (s.ToggleAllHotkey, Ui.T("Hide / show all notes")),
            (s.NotesListHotkey, Ui.T("All notes and archive")),
            (s.PickNotesHotkey, Ui.T("Choose notes to keep")),
            (s.SavedNotesHotkey, Ui.T("Show the kept notes")),
            (s.ApiKeysHotkey, Ui.T("AI languages & keys")),
            (s.FadeHotkey, Ui.T("Fade notes in other apps")),
            (s.HelpHotkey, Ui.T("This guide")),
            (s.QuitHotkey, Ui.T("Quit Open Note")),
        ]);
        AddRows(InNote,
        [
            ("Ctrl+B / I", Ui.T("Bold / italic")),
            ("/-   />", Ui.T("Bullet / checkbox")),
            ("Ctrl+ = / -", Ui.T("Text size")),
            ("Ctrl+G", Ui.T("Check or translate (AI)")),
            ("Ctrl+R", Ui.T("Read aloud")),
            ("Ctrl+Shift+S", Ui.T("Star: important")),
            ("Ctrl+W", Ui.T("Close the note")),
            ("Ctrl+D", Ui.T("Archive the note")),
        ]);
    }

    static void AddRows(Panel panel, (string Keys, string What)[] rows)
    {
        foreach (var (keys, what) in rows)
        {
            if (string.IsNullOrWhiteSpace(keys))
                continue; // a shortcut turned off in settings.json
            var row = new Grid { Margin = new Thickness(0, 0.5, 0, 0.5) };
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
