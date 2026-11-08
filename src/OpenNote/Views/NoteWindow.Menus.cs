using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using OpenNote.Core;
using OpenNote.Native;

namespace OpenNote.Views;

internal partial class NoteWindow
{
    // ---------- Menus ----------

    /// <summary>Right-click in the text: spelling suggestions, AI check, bold/italic, clipboard.</summary>
    void Editor_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var menu = new ContextMenu();
        var pointer = Editor.GetPositionFromPoint(Mouse.GetPosition(Editor), snapToText: true) ?? Editor.CaretPosition;
        if (SpellCheck.GetIsEnabled(Editor) && Editor.GetSpellingError(pointer) is { } error)
        {
            var suggestions = error.Suggestions.Take(5).ToList();
            if (suggestions.Count == 0)
                menu.Items.Add(new MenuItem { Header = Ui.T("No spelling suggestions"), IsEnabled = false });
            foreach (var suggestion in suggestions)
            {
                var item = new MenuItem { Header = suggestion, FontWeight = FontWeights.SemiBold };
                item.Click += (_, _) => error.Correct(suggestion);
                menu.Items.Add(item);
            }
            menu.Items.Add(Item("Ignore", "", error.IgnoreAll));
            menu.Items.Add(new Separator());
        }

        var ai = Item(Editor.Selection.IsEmpty ? "Check this line with AI" : "Check with AI", "Ctrl+G", StartAiCheck);
        ai.Icon = new TextBlock { Text = "", FontFamily = (FontFamily)FindResource("IconFont"), FontSize = 12 };
        menu.Items.Add(ai);
        menu.Items.Add(Item(App.Instance.Speech.IsSpeaking ? "Stop reading" : "Read aloud", "Ctrl+R", ReadAloud));
        menu.Items.Add(new Separator());
        menu.Items.Add(CommandItem("Bold", "Ctrl+B", EditingCommands.ToggleBold));
        menu.Items.Add(CommandItem("Italic", "Ctrl+I", EditingCommands.ToggleItalic));
        menu.Items.Add(TextColorMenu());
        menu.Items.Add(new Separator());
        menu.Items.Add(CommandItem("Cut", "Ctrl+X", ApplicationCommands.Cut));
        menu.Items.Add(CommandItem("Copy", "Ctrl+C", ApplicationCommands.Copy));
        menu.Items.Add(CommandItem("Paste", "Ctrl+V", ApplicationCommands.Paste));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Spell check", "", ToggleSpellCheck, SpellCheckWanted));

        Editor.ContextMenu = menu;
    }

    /// <summary>"Text color" submenu: the colors (the one Ctrl+H puts on shows the key) and "No color".</summary>
    MenuItem TextColorMenu()
    {
        var menu = new MenuItem { Header = Ui.T("Text color"), IsEnabled = !Note.IsLocked };
        foreach (var c in Palette.TextColors)
        {
            var tag = c.Tag;
            var item = Item(c.Name, tag == _lastTextColor ? "Ctrl+H" : "", () => SetTextColor(tag));
            item.Icon = new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(3), Background = RichText.TextBrush(tag, darkPaper: false) }; // menus are light
            menu.Items.Add(item);
        }
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("No color", "", () => SetTextColor(null)));
        return menu;
    }

    MenuItem CommandItem(string header, string gesture, ICommand command) =>
        new() { Header = Ui.T(header), InputGestureText = gesture, Command = command, CommandTarget = Editor };

    // ---------- Header & note menu ----------

    /// <summary>
    /// Drag moves the note; a click on the title that doesn't move it renames the note (after a
    /// short wait, see ScheduleRename); a double-click toggles between the normal size and the last size.
    /// </summary>
    void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed)
            return;
        if (e.ClickCount >= 2)
        {
            CancelPendingRename();
            ToggleSize();
            e.Handled = true;
            return;
        }
        if (TitleEditor.IsVisible)
            return;
        bool onTitle = OnTitle(e);
        var before = Win32.GetBounds(_hwnd);
        BeginDrag();
        try
        {
            DragMove(); // returns when the button is released
        }
        catch (InvalidOperationException)
        {
            // The button was already up: nothing moved, but the drag must still end (its timer stops).
        }
        bool moved = Win32.GetBounds(_hwnd) != before;
        EndDrag(moved);
        if (!moved && onTitle)
            ScheduleRename();
    }

    /// <summary>Width of the header's title column, between the badge and the buttons.</summary>
    double TitleColumnWidth => ((Grid)TitleText.Parent).ColumnDefinitions[1].ActualWidth;

    /// <summary>
    /// True when the click is on the title itself, and never further than half the bar: the
    /// rest of the bar is for grabbing the note, so a click there doesn't start renaming.
    /// </summary>
    bool OnTitle(MouseButtonEventArgs e)
    {
        double x = e.GetPosition(TitleText).X;
        return x >= -4 && x <= Math.Min(TitleText.ActualWidth + 8, TitleColumnWidth / 2);
    }

    void Header_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        OpenMenu(null);
        e.Handled = true;
    }

    void StarButton_Click(object sender, RoutedEventArgs e) => OpenMarkPicker();

    static readonly Brush PickedBrush = Brush(0x2E, (0x1F, 0x2A, 0x5C));
    static readonly Brush HoverBrush = Brush(0x16, (0, 0, 0));

    /// <summary>
    /// The star's little menu: the star or one of the flowers marks the note as important (the one
    /// in use is ringed); ✕ takes the mark off.
    /// </summary>
    void OpenMarkPicker()
    {
        var popup = new Popup
        {
            PlacementTarget = StarButton, Placement = PlacementMode.Bottom, HorizontalOffset = 2,
            StaysOpen = false, AllowsTransparency = true, PopupAnimation = PopupAnimation.Fade,
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(5) };
        string current = !Note.IsImportant ? "-" : NoteMarks.IsFlower(Note.Mark) ? Note.Mark : NoteMarks.Star;

        void Add(UIElement icon, string tip, bool picked, Action choose)
        {
            var face = new Border
            {
                Width = 36, Height = 36, CornerRadius = new CornerRadius(18), Child = icon,
                Background = picked ? PickedBrush : Brushes.Transparent,
            };
            var button = new Button
            {
                Content = face, Margin = new Thickness(1), Padding = new Thickness(0), Focusable = false,
                Cursor = Cursors.Hand, ToolTip = Ui.T(tip), Template = SwatchTemplate,
            };
            button.MouseEnter += (_, _) => face.Background = picked ? PickedBrush : HoverBrush;
            button.MouseLeave += (_, _) => face.Background = picked ? PickedBrush : Brushes.Transparent;
            button.Click += (_, _) =>
            {
                popup.IsOpen = false;
                choose();
            };
            row.Children.Add(button);
        }

        var star = new Path
        {
            Data = StarOutline.Data, Stretch = Stretch.Uniform, Width = 22, Height = 22,
            Fill = Brush(255, (0x1F, 0x2A, 0x5C)), Stroke = Brush(255, (0x1F, 0x2A, 0x5C)),
            StrokeThickness = 1.2, StrokeLineJoin = PenLineJoin.Round,
        };
        Add(star, "Star", current == NoteMarks.Star, () => SetMark(NoteMarks.Star));
        foreach (var (id, name) in NoteMarks.Flowers)
            Add(new Image { Source = FlowerPicture(id), Width = 28, Height = 28 }, name, current == id, () => SetMark(id));
        if (Note.IsImportant)
            Add(new TextBlock { Text = "✕", FontSize = 15, Foreground = Brush(255, (0x5A, 0x50, 0x48)), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
                "Remove the mark", false, ClearMark);

        popup.Child = new Border
        {
            Margin = new Thickness(6, 4, 10, 12), // room for the shadow
            Background = Brush(0xF7, (0xFF, 0xFD, 0xF8)),
            BorderBrush = Brush(0x22, (0, 0, 0)), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(22), Child = row,
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 10, ShadowDepth = 2, Direction = 270, Opacity = 0.22 },
        };
        popup.IsOpen = true;
    }
    void PinButton_Click(object sender, RoutedEventArgs e) => TogglePin();
    void MenuButton_Click(object sender, RoutedEventArgs e) => OpenMenu(MenuButton);
    void CloseButton_Click(object sender, RoutedEventArgs e) => _manager.CloseNote(Note);

    void OpenMenu(UIElement? anchor)
    {
        var menu = new ContextMenu
        {
            PlacementTarget = anchor ?? this,
            Placement = anchor is null ? PlacementMode.MousePoint : PlacementMode.Bottom,
        };

        menu.Items.Add(ColorRow());
        menu.Items.Add(SliderItem(() => Ui.T("Glass tint {0}", Note.Opacity.ToString("P0")), MinOpacity, 1, Note.Opacity, 0.05, Ui.T("Ctrl+[ and Ctrl+]"), SetOpacity));
        menu.Items.Add(SliderItem(() => Ui.T("Text size {0}", Note.FontSize.ToString("0")), MinFont, MaxFont, Note.FontSize, 1, Ui.T("Ctrl+wheel, or Ctrl+= / Ctrl+- (Ctrl+0 resets)"), SetFontSize));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Rename", "F2", BeginTitleEdit));

        var fonts = new MenuItem { Header = Ui.T("Font") };
        fonts.Items.Add(Item("Mono (Consolas)", "", () => SetFont(NoteFont.Mono), Note.Font == NoteFont.Mono));
        fonts.Items.Add(Item("Sans", "", () => SetFont(NoteFont.Sans), Note.Font == NoteFont.Sans));
        fonts.Items.Add(Item("Handwriting", "", () => SetFont(NoteFont.Hand), Note.Font == NoteFont.Hand));
        menu.Items.Add(fonts);

        menu.Items.Add(Item("In saved notes (Ctrl+Alt+P list)", "Ctrl+T", TogglePin, _manager.IsInSelection(Note)));
        menu.Items.Add(Item("Lock editing", "Ctrl+L", ToggleLock, Note.IsLocked));
        menu.Items.Add(Item("Spell check", "", ToggleSpellCheck, SpellCheckWanted));
        menu.Items.Add(Item("Checklist item", "Ctrl+Enter", ToggleChecklistLine));
        menu.Items.Add(Item("Save as file…", "", SaveAsFile));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("New note", "Ctrl+N", () => _manager.CreateNote(near: this)));
        menu.Items.Add(Item("Close note", "Ctrl+W", () => _manager.CloseNote(Note)));
        menu.Items.Add(Item("Archive note", "Ctrl+D", () => _manager.ArchiveNote(Note)));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("All notes…", _manager.Settings.NotesListHotkey, () => App.Instance.ShowNotesList()));

        menu.IsOpen = true;
    }

    /// <summary>
    /// Saves a copy of the note: .onote (everything, to open again in Open Note, here or on another
    /// PC) or .txt (just the text). The note itself stays as it is.
    /// </summary>
    void SaveAsFile()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = NoteFile.SuggestedName(Note),
            Filter = Ui.T("Open Note note (keeps colors and pictures)") + $" (*{NoteFile.Extension})|*{NoteFile.Extension}|"
                     + Ui.T("Text only") + " (*.txt)|*.txt",
            AddExtension = true,
        };
        if (dialog.ShowDialog(this) != true)
            return;
        try
        {
            bool text = dialog.FilterIndex == 2 || dialog.FileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase);
            var bytes = text ? NoteFile.ToTextFile(Note) : NoteFile.ToOnote(Note, ReadPicture);
            if (bytes.Length > NoteFile.MaxBytes) // Open Note wouldn't open it again
            {
                ShowNotice(Ui.T("Couldn't save the file: {0}", Ui.T("the note's pictures are too big for one file.")));
                return;
            }
            NoteFile.WriteFile(dialog.FileName, bytes);
            ShowNotice(Ui.T("Saved as {0}", System.IO.Path.GetFileName(dialog.FileName)));
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            ShowNotice(Ui.T("Couldn't save the file: {0}", ex.Message));
        }
    }

    /// <summary>A picture's bytes; null only when it's gone. A picture that can't be read now stops the save (with the reason), so a file is never quietly missing one.</summary>
    byte[]? ReadPicture(string relativePath) =>
        _manager.Images.FullPath(relativePath) is { } path && System.IO.File.Exists(path) ? System.IO.File.ReadAllBytes(path) : null;

    /// <summary>A row of color swatches (Ctrl+1…8), each drawn as a tiny note with its header.</summary>
    MenuItem ColorRow()
    {
        var row = new WrapPanel { Width = 176, Margin = new Thickness(0, 4, 0, 4) };
        MenuItem? host = null;
        for (int i = 0; i < Palette.Colors.Count; i++)
        {
            var c = Palette.Colors[i];
            bool current = string.Equals(Note.Color, c.Hex, StringComparison.OrdinalIgnoreCase);
            var swatch = new Grid { Width = 18, Height = 18 };
            swatch.Children.Add(new Border { CornerRadius = new CornerRadius(5), Background = Brush(255, Palette.ParseRgb(c.Hex)) });
            swatch.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(5, 5, 0, 0), Height = 5, VerticalAlignment = VerticalAlignment.Top,
                Background = Brush(255, Palette.ParseRgb(c.StripHex)),
            });
            swatch.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(5),
                BorderThickness = new Thickness(current ? 2 : 1),
                BorderBrush = current ? Brush(255, (0x33, 0x33, 0x33)) : Brush(40, (0, 0, 0)),
            });

            var hex = c.Hex;
            var button = new Button
            {
                Content = swatch, Margin = new Thickness(0, 0, 4, 0), Padding = new Thickness(0),
                Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
                ToolTip = $"{Ui.T(c.Name)} (Ctrl+{i + 1})", Focusable = false,
                Template = SwatchTemplate,
            };
            button.Click += (_, _) =>
            {
                SetColor(hex);
                if (host?.Parent is ContextMenu m)
                    m.IsOpen = false;
            };
            row.Children.Add(button);
        }
        host = new MenuItem { Header = row, StaysOpenOnClick = true };
        return host;
    }

    static readonly ControlTemplate SwatchTemplate = new(typeof(Button))
    {
        VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)),
    };

    /// <summary>A labelled slider in the note menu (glass tint, text size); the label follows the value.</summary>
    static MenuItem SliderItem(Func<string> label, double min, double max, double value, double step, string tip, Action<double> set)
    {
        var text = new TextBlock { Text = label(), Margin = new Thickness(0, 0, 0, 2) };
        var slider = new Slider
        {
            Minimum = min, Maximum = max, Value = value, Width = 170,
            SmallChange = step, LargeChange = step * 2, IsMoveToPointEnabled = true,
            ToolTip = tip,
        };
        slider.ValueChanged += (_, e) =>
        {
            set(e.NewValue);
            text.Text = label();
        };
        var panel = new StackPanel { Margin = new Thickness(0, 2, 0, 4) };
        panel.Children.Add(text);
        panel.Children.Add(slider);
        return new MenuItem { Header = panel, StaysOpenOnClick = true };
    }

    static MenuItem Item(string header, string gesture, Action action, bool isChecked = false)
    {
        var item = new MenuItem { Header = Ui.T(header), InputGestureText = gesture, IsChecked = isChecked };
        item.Click += (_, _) => action();
        return item;
    }
}
