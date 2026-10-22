using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
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
                menu.Items.Add(new MenuItem { Header = "No spelling suggestions", IsEnabled = false });
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
        menu.Items.Add(new Separator());
        menu.Items.Add(CommandItem("Cut", "Ctrl+X", ApplicationCommands.Cut));
        menu.Items.Add(CommandItem("Copy", "Ctrl+C", ApplicationCommands.Copy));
        menu.Items.Add(CommandItem("Paste", "Ctrl+V", ApplicationCommands.Paste));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Spell check", "", ToggleSpellCheck, SpellCheckWanted));

        Editor.ContextMenu = menu;
    }

    MenuItem CommandItem(string header, string gesture, ICommand command) =>
        new() { Header = header, InputGestureText = gesture, Command = command, CommandTarget = Editor };

    // ---------- Header & note menu ----------

    /// <summary>
    /// Drag moves the note; a click that doesn't move it renames the note (after a short wait, see
    /// ScheduleRename); a double-click toggles between the normal size and the last size.
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
        var before = Win32.GetBounds(_hwnd);
        DragMove(); // returns when the button is released
        if (Win32.GetBounds(_hwnd) == before)
            ScheduleRename();
    }

    void Header_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        OpenMenu(null);
        e.Handled = true;
    }

    void AddButton_Click(object sender, RoutedEventArgs e) => _manager.CreateNote(near: this);
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
        menu.Items.Add(OpacityItem());
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Rename", "F2", BeginTitleEdit));

        var fonts = new MenuItem { Header = "Font" };
        fonts.Items.Add(Item("Mono (Consolas)", "", () => SetFont(NoteFont.Mono), Note.Font == NoteFont.Mono));
        fonts.Items.Add(Item("Sans", "", () => SetFont(NoteFont.Sans), Note.Font == NoteFont.Sans));
        fonts.Items.Add(Item("Handwriting", "", () => SetFont(NoteFont.Hand), Note.Font == NoteFont.Hand));
        menu.Items.Add(fonts);

        menu.Items.Add(Item("In saved notes (Ctrl+Alt+P list)", "Ctrl+T", TogglePin, _manager.IsInSelection(Note)));
        menu.Items.Add(Item("Lock editing", "Ctrl+L", ToggleLock, Note.IsLocked));
        menu.Items.Add(Item("Spell check", "", ToggleSpellCheck, SpellCheckWanted));
        menu.Items.Add(Item("Checklist item", "Ctrl+Enter", ToggleChecklistLine));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("New note", "Ctrl+N", () => _manager.CreateNote(near: this)));
        menu.Items.Add(Item("Close note", "Ctrl+W", () => _manager.CloseNote(Note)));
        menu.Items.Add(Item("Archive note", "Ctrl+D", () => _manager.ArchiveNote(Note)));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("All notes…", _manager.Settings.NotesListHotkey, () => App.Instance.ShowNotesList()));

        menu.IsOpen = true;
    }

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
                ToolTip = $"{c.Name} (Ctrl+{i + 1})", Focusable = false,
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

    MenuItem OpacityItem()
    {
        var label = new TextBlock { Text = OpacityLabel(), Margin = new Thickness(0, 0, 0, 2) };
        var slider = new Slider
        {
            Minimum = MinOpacity, Maximum = 1, Value = Note.Opacity, Width = 170,
            SmallChange = 0.05, LargeChange = 0.1, IsMoveToPointEnabled = true,
            ToolTip = "Ctrl+[ and Ctrl+]",
        };
        slider.ValueChanged += (_, e) =>
        {
            SetOpacity(e.NewValue);
            label.Text = OpacityLabel();
        };
        var panel = new StackPanel { Margin = new Thickness(0, 2, 0, 4) };
        panel.Children.Add(label);
        panel.Children.Add(slider);
        return new MenuItem { Header = panel, StaysOpenOnClick = true };
    }

    string OpacityLabel() => $"Glass tint {Note.Opacity:P0}";

    static MenuItem Item(string header, string gesture, Action action, bool isChecked = false)
    {
        var item = new MenuItem { Header = header, InputGestureText = gesture, IsChecked = isChecked };
        item.Click += (_, _) => action();
        return item;
    }
}
