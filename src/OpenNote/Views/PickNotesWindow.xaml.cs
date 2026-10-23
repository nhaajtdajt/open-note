using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using OpenNote.Core;
using OpenNote.Native;

namespace OpenNote.Views;

/// <summary>
/// A small keyboard-driven picker: choose which notes stay on screen. Arrow keys move, Space
/// (or a click) selects, Enter confirms, Esc or clicking elsewhere cancels.
/// </summary>
internal partial class PickNotesWindow : Window
{
    bool _confirmed, _closing;

    public PickNotesWindow(IEnumerable<Note> notes, IReadOnlySet<Guid> preselected)
    {
        InitializeComponent();
        var items = notes.Select(n => new NoteItem(n)).ToList();
        List.ItemsSource = items;
        foreach (var item in items.Where(i => preselected.Contains(i.Note.Id)))
            List.SelectedItems.Add(item);

        Loaded += (_, _) =>
        {
            Activate();
            List.UpdateLayout();
            var first = items.FirstOrDefault(i => preselected.Contains(i.Note.Id)) ?? items.FirstOrDefault();
            if (first is not null && List.ItemContainerGenerator.ContainerFromItem(first) is ListBoxItem row)
                row.Focus();
            else
                List.Focus();
        };
        // Clicking elsewhere cancels. Closing (Esc, Enter) deactivates the window too, and calling
        // Close() again while it is already closing throws.
        Closing += (_, _) => _closing = true;
        Deactivated += (_, _) =>
        {
            if (!_confirmed && !_closing)
                Close();
        };
    }

    /// <summary>Raised with the chosen notes when Enter is pressed.</summary>
    public event Action<IReadOnlyList<Note>>? Confirmed;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        Win32.MakeNoteWindow(new WindowInteropHelper(this).Handle); // no Alt+Tab or taskbar entry
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            var chosen = List.SelectedItems.OfType<NoteItem>().Select(i => i.Note).ToList();
            if (chosen.Count == 0)
            {
                Warning.Visibility = Visibility.Visible;
            }
            else
            {
                _confirmed = true;
                Confirmed?.Invoke(chosen);
                Close();
            }
            e.Handled = true;
        }
        else if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control)
        {
            List.SelectAll();
            e.Handled = true;
        }
        base.OnPreviewKeyDown(e);
    }
}
