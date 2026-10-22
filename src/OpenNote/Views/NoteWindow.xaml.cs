using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using OpenNote.Core;
using OpenNote.Native;
using OpenNote.Services;

namespace OpenNote.Views;

internal partial class NoteWindow : Window
{
    const double MinOpacity = 0.2, MinFont = 9, MaxFont = 40;
    const string PinGlyph = "", PinnedGlyph = "";

    // Segoe UI Symbol is listed last so ☐/☑ use its outline glyphs instead of the heavy color-emoji fallback.
    static readonly FontFamily MonoFont = new("Consolas, Cascadia Mono, Segoe UI Symbol");
    static readonly FontFamily SansFont = new("Segoe UI Variable Text, Segoe UI, Segoe UI Symbol");
    static readonly FontFamily HandFont = new("Ink Free, Segoe Print, Segoe UI Symbol");
    static readonly Duration Fade = TimeSpan.FromMilliseconds(140);

    readonly NoteManager _manager;
    readonly PixelPoint? _spawnAt;
    nint _hwnd;
    bool _placed, _closingByManager;
    double _shadowScale = 1;

    public NoteWindow(NoteManager manager, Note note, PixelPoint? spawnAt)
    {
        InitializeComponent();
        _manager = manager;
        _spawnAt = spawnAt;
        Note = note;

        RichText.Load(Editor.Document, note.Text, CreatePicture);
        Editor.IsUndoEnabled = false; // start with an empty undo history, so Ctrl+Z can't "undo" loading the note
        Editor.IsUndoEnabled = true;
        Editor.CaretPosition = Editor.Document.ContentStart; // open at the top of the note
        ApplyTopmost();
        ApplyAppearance();
        UpdateChrome(animate: false);
        UpdatePlaceholder();

        Editor.TextChanged += Editor_TextChanged;
        Editor.PreviewTextInput += Editor_PreviewTextInput;
        Editor.PreviewMouseLeftButtonDown += Editor_PreviewMouseLeftButtonDown;
        Editor.PreviewMouseMove += Editor_PreviewMouseMove;
        Editor.SizeChanged += (_, _) => UpdatePictureWidths();
        Editor.SelectionChanged += (_, _) => UpdatePictureSelection();
        Editor.PreviewMouseWheel += Editor_PreviewMouseWheel;
        Editor.ContextMenuOpening += Editor_ContextMenuOpening;
        DataObject.AddPastingHandler(Editor, Editor_Pasting);
        DataObject.AddCopyingHandler(Editor, Editor_Copying);
        Editor.AddHandler(CommandManager.PreviewExecutedEvent, new ExecutedRoutedEventHandler(Editor_PreviewCommand));
        Editor.PreviewDragOver += Editor_PreviewDragOver;
        Editor.PreviewDrop += Editor_PreviewDrop;
        TitleEditor.PreviewKeyDown += TitleEditor_PreviewKeyDown;
        TitleEditor.LostKeyboardFocus += (_, _) => EndTitleEdit(commit: true);

        LocationChanged += (_, _) => SaveBounds();
        SizeChanged += (_, _) => SaveBounds();
        Activated += (_, _) =>
        {
            _manager.NoteActivated(Note);
            UpdateChrome();
            UpdateSpellCheck();
        };
        Deactivated += (_, _) =>
        {
            UpdateChrome();
            UpdateSpellCheck();
        };
        MouseEnter += (_, _) => UpdateChrome();
        MouseLeave += (_, _) => UpdateChrome();
        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
        };
    }

    public Note Note { get; }

    Paragraph? CaretParagraph => Editor.CaretPosition.Paragraph;

    // ---------- Native window setup & placement ----------

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        Win32.MakeNoteWindow(_hwnd);
        Place();
        _placed = true;
        SaveBounds();
    }

    /// <summary>Runs before the window is first shown, so the note appears directly in place.</summary>
    void Place()
    {
        var areas = Displays.WorkAreas();
        if (Note.Width > 0 && Note.Height > 0)
        {
            var target = ScreenMath.EnsureVisible(new PixelRect(Note.X, Note.Y, Note.Width, Note.Height), areas);
            Win32.SetBounds(_hwnd, target);
            // If the note lives on a monitor with different scaling, WPF rescales it on the
            // first move; setting the bounds again restores the exact saved pixel size.
            Win32.SetBounds(_hwnd, target);
            return;
        }

        // New note: keep the default size (in DIPs) and put it where it was asked for, fully on one monitor.
        if (_spawnAt is { } at && Win32.GetBounds(_hwnd) is { } current)
        {
            var area = ScreenMath.AreaFor(at, areas);
            var target = ScreenMath.ClampInto(current with { X = at.X, Y = at.Y }, area);
            Win32.SetBounds(_hwnd, target, moveOnly: true);
        }
    }

    void SaveBounds()
    {
        if (!_placed || WindowState != WindowState.Normal || Win32.GetBounds(_hwnd) is not { Width: > 0 } r)
            return;
        if (r.X == Note.X && r.Y == Note.Y && r.Width == Note.Width && r.Height == Note.Height)
            return;
        // Resizing by hand while shrunk starts a new "last size": forget the one to restore.
        if (!_resizingByToggle && Note.RestoreWidth > 0 && (r.Width != Note.Width || r.Height != Note.Height))
            (Note.RestoreWidth, Note.RestoreHeight) = (0, 0);
        (Note.X, Note.Y, Note.Width, Note.Height) = (r.X, r.Y, r.Width, r.Height);
        _manager.NoteMoved();
    }

    /// <summary>Raises the note above other apps without stealing focus; brings back a lost note.</summary>
    public void BringToFront()
    {
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        if (!IsVisible)
            Show();

        var areas = Displays.WorkAreas();
        if (Win32.GetBounds(_hwnd) is { } r && ScreenMath.EnsureVisible(r, areas) is var fixedRect && fixedRect != r)
            Win32.SetBounds(_hwnd, fixedRect);

        if (!Topmost)
            Win32.RaiseToTop(_hwnd);
    }

    public void ActivateAndFocus()
    {
        Activate();
        Editor.Focus();
    }

    public void CloseFromManager()
    {
        _closingByManager = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Alt+F4 and similar: close the note (it stays in the list), never lose it.
        if (!_closingByManager && !_manager.IsShuttingDown)
        {
            e.Cancel = true;
            Dispatcher.BeginInvoke(() => _manager.CloseNote(Note));
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _aiCancel?.Cancel();
        base.OnClosed(e);
    }

    // ---------- Text ----------

    void Editor_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Also fires for formatting changes (Ctrl+B) and for font/color changes; only real
        // differences in the stored text count as an edit.
        var text = RichText.Save(Editor.Document);
        if (text == Note.Text)
            return;
        Note.Text = text;
        UpdatePlaceholder();
        UpdateSpellCheck();
        _manager.NoteEdited(Note);
    }

    /// <summary>
    /// Paste keeps bold and italic (from other notes, Word, WordPad...) but drops every other
    /// kind of formatting, so pasted text always matches the note.
    /// </summary>
    void Editor_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        e.CancelCommand();
        if (Note.IsLocked || TryInsertPictures(e.DataObject, dropAt: null))
            return;

        List<StyledSpan>? spans = null;
        foreach (var format in new[] { DataFormats.Xaml, DataFormats.Rtf })
        {
            if (!e.DataObject.GetDataPresent(format) || e.DataObject.GetData(format) is not string content)
                continue;
            try
            {
                var doc = new FlowDocument();
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
                new TextRange(doc.ContentStart, doc.ContentEnd).Load(stream, format);
                spans = RichText.Spans(doc);
                break;
            }
            catch (Exception ex) when (ex is ArgumentException or System.Windows.Markup.XamlParseException or IOException)
            {
                // Malformed rich data: fall back to the plain text below.
            }
        }
        if (spans is null)
        {
            if (e.DataObject.GetData(DataFormats.UnicodeText) is not string text)
                return;
            spans = [new StyledSpan(text, false, false)];
        }
        InsertSpans(spans);
    }

    void InsertSpans(List<StyledSpan> spans)
    {
        Editor.BeginChange();
        try
        {
            RichText.RemovePictures(Editor, Editor.Selection.Start, Editor.Selection.End);
            Editor.Selection.Text = "";
            var position = Editor.Selection.Start;
            foreach (var span in spans)
            {
                var range = new TextRange(position, position) { Text = span.Text };
                range.ApplyPropertyValue(TextElement.FontWeightProperty, span.Bold ? FontWeights.Bold : FontWeights.Normal);
                range.ApplyPropertyValue(TextElement.FontStyleProperty, span.Italic ? FontStyles.Italic : FontStyles.Normal);
                position = range.End;
            }
            Editor.Selection.Select(position, position);
        }
        finally
        {
            Editor.EndChange();
        }
    }

    // ---------- Appearance ----------

    /// <summary>
    /// "Liquid glass": a tinted, see-through body with a specular sheen, a bright inner rim and
    /// a dark hairline edge. Opacity controls how much tint the glass carries; text stays solid.
    /// </summary>
    void ApplyAppearance()
    {
        double opacity = Math.Clamp(Note.Opacity, MinOpacity, 1);
        var alpha = (byte)Math.Round(opacity * 255);
        bool dark = Palette.IsDark(Note.Color);
        var paper = Palette.ParseRgb(Note.Color);
        var strip = Palette.Strip(Note.Color);
        var ink = Palette.Ink(Note.Color);
        (byte R, byte G, byte B) white = (255, 255, 255), black = (0, 0, 0);

        Glass.Background = Brush(alpha, paper);
        Header.Background = Brush((byte)(alpha * 0.55), strip);
        Header.BorderBrush = Brush(dark ? (byte)0x22 : (byte)0x14, dark ? white : ink);
        Edge.BorderBrush = Brush(dark ? (byte)0x66 : (byte)0x2E, dark ? black : Palette.Mix(paper, black, 0.55));

        // Glass catches more light the clearer it is; dark glass reflects less.
        double shine = dark ? 0.45 : 1;
        SheenTop.Color = Color.FromArgb((byte)(0x70 * shine), 255, 255, 255);
        RimTop.Color = Color.FromArgb((byte)(0xD9 * shine), 255, 255, 255);

        Foreground = Brush(255, ink);
        Editor.Document.Foreground = Foreground;
        Editor.SelectionBrush = Brush(255, Palette.Mix(strip, dark ? white : black, 0.35));
        TitleEditor.Background = Brush(dark ? (byte)0x30 : (byte)0x70, dark ? black : white);

        // See-through glass shouldn't cast a solid shadow: fade the shadow out as the tint fades.
        Shadow.Color = dark ? Colors.Black : Color.FromRgb(0x2A, 0x24, 0x10);
        _shadowScale = Math.Clamp((opacity - 0.3) / 0.6, 0.25, 1);

        var family = Note.Font switch
        {
            NoteFont.Sans => SansFont,
            NoteFont.Hand => HandFont,
            _ => MonoFont,
        };
        // Ink Free runs small and Consolas wide; nudge sizes so all three read alike.
        double scale = Note.Font switch { NoteFont.Hand => 1.15, NoteFont.Mono => 0.98, _ => 1 };
        double size = Math.Clamp(Note.FontSize, MinFont, MaxFont) * scale;
        Placeholder.FontFamily = TitleText.FontFamily = TitleEditor.FontFamily = family;
        Editor.FontFamily = Editor.Document.FontFamily = family;
        Placeholder.FontSize = Editor.FontSize = Editor.Document.FontSize = size;
        Editor.Document.LineHeight = size * 1.45;
        Editor.Document.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        TitleText.FontSize = TitleEditor.FontSize = Math.Max(12, size * 0.95);
    }

    static SolidColorBrush Brush(byte alpha, (byte R, byte G, byte B) c)
    {
        var brush = new SolidColorBrush(Color.FromArgb(alpha, c.R, c.G, c.B));
        brush.Freeze();
        return brush;
    }

    /// <summary>
    /// Header buttons fade in only while the note is hovered or focused, and the glass "lifts"
    /// (deeper shadow) while you're working in it; at rest a note is just glass, title and text.
    /// </summary>
    void UpdateChrome(bool animate = true)
    {
        bool engaged = IsActive || IsMouseOver;
        FadeTo(AddButton, engaged ? 0.75 : 0, animate);
        FadeTo(MenuButton, engaged ? 0.75 : 0, animate);
        FadeTo(CloseButton, engaged ? 0.75 : 0, animate);
        bool saved = _manager.IsInSelection(Note);
        FadeTo(PinButton, saved ? 0.75 : engaged ? 0.5 : 0, animate);
        PinButton.Content = saved ? PinnedGlyph : PinGlyph;
        PinButton.ToolTip = saved ? "Remove from your saved notes (Ctrl+T)" : "Add to your saved notes (Ctrl+T)";

        bool untitled = string.IsNullOrWhiteSpace(Note.Heading);
        TitleText.Text = untitled ? "Untitled" : Note.Heading;
        TitleText.Opacity = untitled ? (engaged && !Note.IsLocked ? 0.4 : 0) : 0.9;
        Header.ToolTip = Note.IsLocked ? null : "Click to rename, drag to move";

        LockGlyph.Visibility = Note.IsLocked ? Visibility.Visible : Visibility.Collapsed;
        LockGlyph.Opacity = 0.55;
        Editor.IsReadOnly = Note.IsLocked;
        Editor.IsReadOnlyCaretVisible = false;

        Shadow.BlurRadius = IsActive ? 20 : 12;
        Shadow.ShadowDepth = IsActive ? 5 : 3;
        Shadow.Opacity = (IsActive ? 0.28 : 0.17) * _shadowScale;
    }

    static void FadeTo(UIElement element, double opacity, bool animate)
    {
        if (!animate || !SystemParameters.ClientAreaAnimation)
        {
            element.BeginAnimation(OpacityProperty, null);
            element.Opacity = opacity;
            return;
        }
        element.BeginAnimation(OpacityProperty, new DoubleAnimation(opacity, Fade));
    }

    void UpdatePlaceholder() =>
        Placeholder.Visibility = Note.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

    void SetColor(string hex)
    {
        Note.Color = hex;
        ApplyAppearance();
        UpdateChrome(animate: false);
        _manager.NoteEdited(Note);
    }

    void SetOpacity(double value)
    {
        Note.Opacity = Math.Round(Math.Clamp(value, MinOpacity, 1), 2);
        ApplyAppearance();
        UpdateChrome(animate: false);
        _manager.NoteMoved();
    }

    void ChangeFontSize(double delta)
    {
        Note.FontSize = delta == 0 ? _manager.Settings.DefaultFontSize : Math.Clamp(Note.FontSize + delta, MinFont, MaxFont);
        ApplyAppearance();
        _manager.NoteMoved();
    }

    void SetFont(NoteFont font)
    {
        Note.Font = font;
        ApplyAppearance();
        _manager.NoteMoved();
    }

    /// <summary>Notes stay above other windows when "Keep notes on top" is on.</summary>
    public void ApplyTopmost() => Topmost = _manager.Settings.KeepNotesOnTop;

    /// <summary>Refreshes the header buttons (the pin shows whether the note is in the saved selection).</summary>
    public void RefreshChrome() => UpdateChrome(animate: false);

    /// <summary>The pin: adds this note to the saved selection (Ctrl+Alt+P) or removes it.</summary>
    void TogglePin() => _manager.ToggleInSelection(Note);

    void ToggleLock()
    {
        Note.IsLocked = !Note.IsLocked;
        UpdateChrome();
        UpdateSpellCheck();
        _manager.NoteEdited(Note);
    }

    // ---------- Spell check ----------

    bool SpellCheckWanted => Note.SpellCheck ?? !LanguageHint.LooksVietnamese(Note.Heading + "\n" + Note.Text);

    bool _spellCheckStarted, _spellCheckBroken, _spellCheckQueued;

    /// <summary>
    /// Windows' spell checker (the one Notepad uses) starts the first time you work in a note,
    /// so notes you never touch cost nothing, and then stays on. It is switched off only when it
    /// shouldn't run: Vietnamese notes in automatic mode (the English dictionary would underline
    /// word by word), locked notes, or when you turn it off.
    /// <para>
    /// It isn't toggled on every focus change: doing that made WPF's own spell checker throw
    /// (NullReferenceException in Speller.EnsureInitialized during window activation).
    /// </para>
    /// </summary>
    void UpdateSpellCheck()
    {
        if (_spellCheckBroken || _spellCheckQueued)
            return;
        _spellCheckQueued = true;
        // Apply after activation has finished, not from inside WPF's own activation handling.
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            _spellCheckQueued = false;
            if (IsActive)
                _spellCheckStarted = true;
            bool on = _spellCheckStarted && !Note.IsLocked && SpellCheckWanted;
            if (SpellCheck.GetIsEnabled(Editor) == on)
                return;
            try
            {
                SpellCheck.SetIsEnabled(Editor, on);
            }
            catch (Exception ex) when (ex is NullReferenceException or System.Runtime.InteropServices.COMException or InvalidOperationException)
            {
                // A fault inside WPF/Windows spell checking: give up on it for this note rather
                // than fail again on every keystroke. Everything else keeps working.
                _spellCheckBroken = true;
            }
        });
    }

    void ToggleSpellCheck()
    {
        Note.SpellCheck = !SpellCheckWanted;
        _spellCheckStarted = true;
        UpdateSpellCheck();
        _manager.NoteMoved();
    }

    // ---------- Title ----------

    void BeginTitleEdit()
    {
        if (Note.IsLocked)
            return;
        Activate();
        TitleEditor.Text = Note.Heading;
        TitleText.Visibility = Visibility.Hidden;
        TitleEditor.Visibility = Visibility.Visible;
        TitleEditor.Focus();
        TitleEditor.SelectAll();
    }

    void EndTitleEdit(bool commit)
    {
        if (TitleEditor.Visibility != Visibility.Visible)
            return;
        TitleEditor.Visibility = Visibility.Collapsed;
        TitleText.Visibility = Visibility.Visible;
        var heading = TitleEditor.Text.Trim();
        if (commit && heading != Note.Heading)
        {
            Note.Heading = heading;
            _manager.NoteEdited(Note);
        }
        UpdateChrome(animate: false);
    }

    void TitleEditor_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Tab or Key.Down)
        {
            EndTitleEdit(commit: true);
            Editor.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            EndTitleEdit(commit: false);
            Editor.Focus();
            e.Handled = true;
        }
    }

    // ---------- Lists & quick inserts ----------
    // The list logic works on a paragraph's plain text (Checklist, QuickInsert); RichText maps
    // its character offsets back to positions in the document.

    /// <summary>Applies a plain-text edit to one paragraph; inserted list markers are never bold/italic.</summary>
    void ApplyLineEdit(Paragraph p, TextEdit edit, bool moveCaret = true)
    {
        var range = new TextRange(RichText.PositionAt(p, edit.Start), RichText.PositionAt(p, edit.Start + edit.Length))
        {
            Text = edit.Replacement,
        };
        if (edit.Replacement.Length > 0)
        {
            range.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Normal);
            range.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Normal);
        }
        if (moveCaret)
            Editor.CaretPosition = RichText.PositionAt(p, edit.CaretAfter);
    }

    void ToggleChecklistLine()
    {
        if (Note.IsLocked || CaretParagraph is not { } p)
            return;
        ApplyLineEdit(p, Checklist.Toggle(RichText.PlainText(p), RichText.CharOffset(p, Editor.CaretPosition)));
    }

    /// <summary>"/-" becomes a bullet and "/>" a checkbox as you type.</summary>
    void Editor_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (Note.IsLocked || !Editor.Selection.IsEmpty || CaretParagraph is not { } p)
            return;
        var edit = QuickInsert.TryExpand(RichText.PlainText(p), RichText.CharOffset(p, Editor.CaretPosition), e.Text);
        if (edit is { } quick)
        {
            ApplyLineEdit(p, quick);
            e.Handled = true;
        }
    }

    /// <summary>Enter on a list line starts the next item; on an empty item it ends the list.</summary>
    bool ContinueList()
    {
        if (Note.IsLocked || !Editor.Selection.IsEmpty || CaretParagraph is not { } p)
            return false;
        var edit = Checklist.ContinueOnEnter(RichText.PlainText(p), RichText.CharOffset(p, Editor.CaretPosition), "\n");
        if (edit is not { } next)
            return false;

        if (next.Replacement.Length == 0)
        {
            ApplyLineEdit(p, next);
            return true;
        }

        Editor.BeginChange();
        try
        {
            EditingCommands.EnterParagraphBreak.Execute(null, Editor);
            var marker = new TextRange(Editor.CaretPosition, Editor.CaretPosition) { Text = next.Replacement.TrimStart('\n') };
            marker.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Normal);
            marker.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Normal);
            Editor.CaretPosition = marker.End;
        }
        finally
        {
            Editor.EndChange();
        }
        return true;
    }

    /// <summary>The paragraph and line start of a checkbox under the mouse, if any.</summary>
    (Paragraph Paragraph, int LineStart)? BoxUnderMouse(MouseEventArgs e)
    {
        if (Editor.GetPositionFromPoint(e.GetPosition(Editor), snapToText: false) is not { Paragraph: { } p } pointer)
            return null;
        var text = RichText.PlainText(p);
        int offset = RichText.CharOffset(p, pointer);
        int lineStart = offset == 0 ? 0 : text.LastIndexOf('\n', offset - 1) + 1;
        // The pointer lands before or after the glyph depending on which half was clicked.
        return offset - lineStart <= 1 && Checklist.IsBoxAt(text, lineStart) ? (p, lineStart) : null;
    }

    void Editor_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Pictures: click selects (then Delete/Backspace/Ctrl+C work on it), double-click opens.
        if (PictureUnderMouse(e) is { } picture)
        {
            if (e.ClickCount >= 2 && picture.Child is PictureView view)
                OpenPicture(view.File);
            else
            {
                Editor.Focus();
                Editor.Selection.Select(picture.ElementStart, picture.ElementEnd);
            }
            e.Handled = true;
            return;
        }

        if (Note.IsLocked || BoxUnderMouse(e) is not { } box)
            return;
        var caret = Editor.CaretPosition;
        ApplyLineEdit(box.Paragraph, Checklist.Toggle(RichText.PlainText(box.Paragraph), box.LineStart), moveCaret: false);
        Editor.CaretPosition = caret;
        e.Handled = true;
    }

    void Editor_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        bool overPicture = PictureUnderMouse(e) is not null;
        Editor.Cursor = overPicture || (!Note.IsLocked && BoxUnderMouse(e) is not null) ? Cursors.Hand : null;
        Editor.ToolTip = overPicture ? "Click to select, double-click to open" : null;
    }

    void Editor_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control)
            return;
        ChangeFontSize(e.Delta > 0 ? 1 : -1);
        e.Handled = true;
    }

    // ---------- Keyboard ----------

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (TitleEditor.IsKeyboardFocusWithin)
        {
            base.OnPreviewKeyDown(e); // the title box handles its own keys
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var mods = Keyboard.Modifiers;

        if (key == Key.Escape && _aiPopup is { IsOpen: true })
        {
            _aiPopup.IsOpen = false;
            e.Handled = true;
        }
        else if (mods == ModifierKeys.Control)
            e.Handled = HandleCtrlShortcut(key);
        else if (mods == (ModifierKeys.Control | ModifierKeys.Shift))
            e.Handled = BlocksRichShortcut(key);
        else if (mods == ModifierKeys.None && key == Key.F2)
        {
            BeginTitleEdit();
            e.Handled = true;
        }
        else if (mods == ModifierKeys.None && key == Key.Enter && ContinueList())
            e.Handled = true;

        base.OnPreviewKeyDown(e);
    }

    bool HandleCtrlShortcut(Key key)
    {
        switch (key)
        {
            case Key.N: _manager.CreateNote(near: this); break;
            case Key.W: _manager.CloseNote(Note); break;
            case Key.D: _manager.ArchiveNote(Note); break;
            case Key.T: TogglePin(); break;
            case Key.L: ToggleLock(); break;
            case Key.G: StartAiCheck(); break;
            case Key.R: ReadAloud(); break;
            case Key.Enter: ToggleChecklistLine(); break;
            case Key.OemOpenBrackets: SetOpacity(Note.Opacity - 0.1); break;
            case Key.OemCloseBrackets: SetOpacity(Note.Opacity + 0.1); break;
            case Key.OemPlus or Key.Add: ChangeFontSize(1); break;
            case Key.OemMinus or Key.Subtract: ChangeFontSize(-1); break;
            case Key.D0 or Key.NumPad0: ChangeFontSize(0); break;
            case >= Key.D1 and <= Key.D8: SetColor(Palette.Colors[key - Key.D1].Hex); break;
            // Underline and paragraph alignment can't be stored; swallow them rather than lose them on save.
            case Key.U or Key.E or Key.J: break;
            default: return false; // let the editor handle Ctrl+B, Ctrl+I, Ctrl+C, Ctrl+Z, ...
        }
        return true;
    }

    /// <summary>
    /// RichTextBox's Ctrl+Shift formatting (bullet/numbered lists, superscript, font size) can't
    /// be stored either; keep only redo and selection keys.
    /// </summary>
    static bool BlocksRichShortcut(Key key) =>
        key is not (Key.Z or Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End
            or Key.PageUp or Key.PageDown or Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl);

}
