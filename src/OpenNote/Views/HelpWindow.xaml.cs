using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
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
    static readonly Brush KeyBrush = Frozen(Color.FromRgb(0x6B, 0x4E, 0x3D)); // warm brown ink
    static readonly Brush TextBrush = Frozen(Color.FromRgb(0x3A, 0x31, 0x28));
    static readonly Brush HeadingBrush = Frozen(Color.FromRgb(0x4A, 0x5E, 0x32)); // the daisy leaves' olive
    const string FeedbackAddress = "nhatdat2905@gmail.com";
    // R. City - Locked Away ft. Adam Levine on YouTube (the link the author chose; a mix plays on after it).
    const string SongUrl = "https://www.youtube.com/watch?v=Fdcw4Mby2RI&list=RDFdcw4Mby2RI&start_radio=1";
    const string SongName = "Locked Away";

    readonly AppSettings _settings;
    readonly Action<Core.Language> _setLanguage;
    readonly Action<Action<AppSettings>> _changeSettings;
    bool _closing, _choosing, _styleEdited;

    /// <param name="setLanguage">Changes the app's display language (and remembers it).</param>
    /// <param name="changeSettings">Changes settings and saves them (the AI agents page).</param>
    public HelpWindow(AppSettings s, Action<Core.Language> setLanguage, Action<Action<AppSettings>> changeSettings)
    {
        InitializeComponent();
        _settings = s;
        _setLanguage = setLanguage;
        _changeSettings = changeSettings;
        StyleBox.Text = s.NoteStyle;
        AllowBox.IsChecked = s.AllowAgents;
        Fill();

        StyleBox.TextChanged += (_, _) =>
        {
            _styleEdited = true;
            StylePlaceholder.Visibility = StyleBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        };
        // Checked/Unchecked rather than Click: also fires for keyboard and screen-reader toggles.
        AllowBox.Checked += (_, _) => _changeSettings(x => x.AllowAgents = true);
        AllowBox.Unchecked += (_, _) => _changeSettings(x => x.AllowAgents = false);

        // Closing deactivates the window too, so guard against closing twice.
        Closing += (_, _) =>
        {
            _closing = true;
            SaveStyle();
        };
        Deactivated += (_, _) =>
        {
            if (!_choosing) // the language list is open
                CloseOnce();
        };
        // On the AI page a click is for typing and ticking; Esc or a click outside closes it.
        MouseLeftButtonUp += (_, _) =>
        {
            if (MenuCard.Visibility == Visibility.Visible)
                ShowMenuCard(false); // a click beside the letter puts it away first
            else if (AiPage.Visibility != Visibility.Visible)
                CloseOnce();
        };
        KeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape)
                return;
            if (MenuCard.Visibility == Visibility.Visible)
                ShowMenuCard(false);
            else
                CloseOnce();
        };
        Loaded += (_, _) =>
        {
            Appear();
            StartSwing();
        };
        // Endless animations keep ticking until the closed window is garbage-collected: stop them.
        Closed += (_, _) =>
        {
            Swing.BeginAnimation(RotateTransform.AngleProperty, null);
            LetterTilt.BeginAnimation(RotateTransform.AngleProperty, null);
        };
    }

    void CloseOnce()
    {
        if (!_closing)
            Close();
    }

    /// <summary>The note style is saved when the card closes, and only if it was edited here (an agent may have just set it).</summary>
    void SaveStyle()
    {
        var style = StyleBox.Text.Trim();
        if (_styleEdited && style != _settings.NoteStyle.Trim())
            _changeSettings(x => x.NoteStyle = style);
        _styleEdited = false;
    }

    /// <summary>Switches between the shortcuts and the AI agents page.</summary>
    void AiButton_Click(object sender, RoutedEventArgs e)
    {
        bool ai = AiPage.Visibility != Visibility.Visible;
        AiPage.Visibility = ai ? Visibility.Visible : Visibility.Collapsed;
        KeysPage.Visibility = Tips.Visibility = ai ? Visibility.Collapsed : Visibility.Visible;
        Fill();
    }

    void CopyAsk_Click(object sender, RoutedEventArgs e) => Copy(CopyAsk, AskPrompt.Text);
    void CopyLearn_Click(object sender, RoutedEventArgs e) => Copy(CopyLearn, LearnPrompt.Text);

    /// <summary>Puts a suggested prompt on the clipboard, and says so on the button for a moment.</summary>
    static void Copy(Button button, string text)
    {
        try
        {
            Clipboard.SetText(text);
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            return; // another app is holding the clipboard
        }
        button.Content = Ui.T("Copied ✓");
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            button.Content = Ui.T("Copy");
        };
        timer.Start();
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
        LanguageButton.Content = Ui.Current.NativeName + "  ▾";
        bool ai = AiPage.Visibility == Visibility.Visible;
        AiButton.Content = ai ? Ui.T("← Shortcuts") : Ui.T("AI agents");
        Letter.ToolTip = Ui.T("Open the letter");
        ListenButton.Content = Ui.T("♪ Listen to {0}", SongName);
        ListenButton.ToolTip = Ui.T("Opens the song on YouTube");
        AiIntro.Text = Ui.T("AI agents that can run commands on this PC (Claude Code, Codex, Cursor…) can read your notes and add new ones. Start your request with one of these:");
        AskLabel.Text = Ui.T("Ask");
        LearnLabel.Text = Ui.T("Learn my style");
        AskPrompt.Text = Ui.T("My PC runs Open Note, a sticky notes app. Run the command opennote-ai to see how to use it, then:");
        LearnPrompt.Text = Ui.T("My PC runs Open Note, a sticky notes app. Run the command opennote-ai, read all my notes, then save a short description of how I write notes with opennote-ai style --set.");
        AskPrompt.ToolTip = AskPrompt.Text;
        LearnPrompt.ToolTip = LearnPrompt.Text;
        CopyAsk.Content = CopyLearn.Content = Ui.T("Copy");
        StyleLabel.Text = Ui.T("My note style (AI agents follow it; empty: the default below)");
        StylePlaceholder.Text = Ui.T(AgentCommands.DefaultStyle);
        StylePlaceholder.Visibility = StyleBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        AllowBox.Content = Ui.T("Let AI agents read and add notes");
        AnywhereHeading.Text = Ui.T("Anywhere");
        InNoteHeading.Text = Ui.T("In a note");
        // Word joiners keep the shortcut on one line.
        Tips.Text = Ui.T("Drag a note by its top bar · throw it on the bin to archive · ☆ marks it important · with {0} on, notes turn see-through while you use other apps; click one to bring them back.",
            s.FadeHotkey.Replace("+", "⁠+⁠"));
        // The address stands out in the headings' olive.
        var feedback = Ui.T("If you have any thoughts or feedback, I'd love to hear from you at {0}, call me Gem or NhatDat <3").Split("{0}");
        Feedback.Inlines.Clear();
        Feedback.Inlines.Add(feedback[0]);
        if (feedback.Length > 1)
        {
            Feedback.Inlines.Add(new System.Windows.Documents.Run(FeedbackAddress) { Foreground = HeadingBrush, FontWeight = FontWeights.SemiBold });
            Feedback.Inlines.Add(feedback[1]);
        }
        CloseHint.Text = ai ?Ui.T("Esc or a click outside closes it; your note style is saved") : Ui.T("Click anywhere or press Esc to close");

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
            ("Ctrl+H", Ui.T("Text color")),
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
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "Keys", MinWidth = 112 });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.Children.Add(new TextBlock { Text = keys, FontFamily = KeyFont, FontSize = 14, FontWeight = FontWeights.Bold, Foreground = KeyBrush, Margin = new Thickness(0, 0, 10, 0) });
            var text = new TextBlock { Text = what, FontFamily = TextFont, FontSize = 15, Foreground = TextBrush, TextTrimming = TextTrimming.CharacterEllipsis };
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
        if (Win32.GetBounds(hwnd) is not { } size)
            return;
        // A small or highly scaled screen (1366×768 at 125%…): shrink the whole card to fit, rather
        // than push its top (the language and AI buttons) off the screen.
        double fit = Math.Min(1, Math.Min((area.Width - 16.0) / size.Width, (area.Height - 16.0) / size.Height));
        if (fit < 1)
        {
            Root.LayoutTransform = new ScaleTransform(fit, fit);
            Width *= fit;
            Height *= fit;
            size = size with { Width = (int)(size.Width * fit), Height = (int)(size.Height * fit) };
        }
        Win32.SetBounds(hwnd, size with { X = area.X + (area.Width - size.Width) / 2, Y = area.Y + (area.Height - size.Height) / 2 }, moveOnly: true);
    }

    /// <summary>
    /// The bag of flowers sways gently on its strap while the card is open. At 15 frames a second:
    /// the card is a see-through window drawn in software, and a slow sway doesn't need more.
    /// </summary>
    void StartSwing()
    {
        if (!SystemParameters.ClientAreaAnimation)
            return;
        Swing.Angle = -2.5;
        var sway = new DoubleAnimation(-2.5, 2.5, TimeSpan.FromSeconds(2.2))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        };
        Timeline.SetDesiredFrameRate(sway, 15);
        Swing.BeginAnimation(RotateTransform.AngleProperty, sway);

        // Every 4 seconds the letter gives a quick shake, so it's clear there is something to open.
        var nudge = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(4), RepeatBehavior = RepeatBehavior.Forever };
        foreach (var (angle, seconds) in new[] { (-12.0, 2.8), (-21.0, 3.0), (-3.0, 3.25), (-16.0, 3.45), (-12.0, 3.65) })
            nudge.KeyFrames.Add(new EasingDoubleKeyFrame(angle, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(seconds)), new SineEase()));
        Timeline.SetDesiredFrameRate(nudge, 15);
        LetterTilt.BeginAnimation(RotateTransform.AngleProperty, nudge);
    }

    void Letter_MouseEnter(object sender, MouseEventArgs e) => GrowLetter(1.15);
    void Letter_MouseLeave(object sender, MouseEventArgs e) => GrowLetter(1);

    void GrowLetter(double scale)
    {
        if (!SystemParameters.ClientAreaAnimation)
        {
            LetterHover.ScaleX = LetterHover.ScaleY = scale;
            return;
        }
        var grow = new DoubleAnimation(scale, TimeSpan.FromMilliseconds(120)) { EasingFunction = new QuadraticEase() };
        LetterHover.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        LetterHover.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
    }

    void Bag_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true; // a click on the bag doesn't close the card
        ShowMenuCard(MenuCard.Visibility != Visibility.Visible);
    }

    void MenuCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => e.Handled = true;

    /// <summary>The song that goes with the letter, opened on YouTube in the browser.</summary>
    void ListenButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(SongUrl) { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // No browser: the address goes on the clipboard instead, and the button says so.
            try
            {
                Clipboard.SetText(SongUrl);
                ListenButton.Content = Ui.T("Copied ✓");
            }
            catch (System.Runtime.InteropServices.ExternalException) { }
        }
    }

    /// <summary>The letter that comes out of the bag (a thank-you note on a photo of flowers); it grows from the bag's side.</summary>
    void ShowMenuCard(bool show)
    {
        MenuCard.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (show)
        {
            // Found it: the letter stops asking to be opened.
            LetterTilt.BeginAnimation(RotateTransform.AngleProperty, null);
            LetterTilt.Angle = -12;
        }
        if (!show || !SystemParameters.ClientAreaAnimation)
            return;
        var ease = new QuarticEase { EasingMode = EasingMode.EaseOut };
        var time = TimeSpan.FromMilliseconds(170);
        MenuCard.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, time) { EasingFunction = ease });
        var grow = new DoubleAnimation(0.85, 1, time) { EasingFunction = ease };
        MenuPop.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        MenuPop.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
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
