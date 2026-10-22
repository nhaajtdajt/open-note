using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using OpenNote.Core;
using OpenNote.Services;

namespace OpenNote.Views;

/// <summary>Find, reopen, hide, archive, restore and permanently delete notes.</summary>
internal partial class NotesListWindow : Window
{
    readonly NoteManager _manager;
    readonly DispatcherTimer _refreshTimer;

    public NotesListWindow(NoteManager manager)
    {
        InitializeComponent();
        _manager = manager;

        // Typing in a note raises a change per keystroke; redraw the list at most a few times a second.
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _refreshTimer.Tick += (_, _) =>
        {
            _refreshTimer.Stop();
            Refresh();
        };
        _manager.NotesChanged += OnNotesChanged;
        Closed += (_, _) =>
        {
            _manager.NotesChanged -= OnNotesChanged;
            _refreshTimer.Stop();
        };

        var s = manager.Settings;
        FooterHint.Text = $"Double-click a note to open it. Press Delete to archive. {s.ToggleAllHotkey} shows or hides your notes from anywhere.";
        Refresh();
    }

    bool ShowingArchive => ArchiveTab.IsChecked == true;

    void OnNotesChanged()
    {
        _refreshTimer.Stop();
        _refreshTimer.Start();
    }

    void Refresh()
    {
        var selectedId = (List.SelectedItem as NoteItem)?.Note.Id;
        var query = SearchBox.Text;
        var notes = _manager.Notes;

        NotesTab.Content = $"Notes  {notes.Count(n => n.State != NoteState.Archived)}";
        ArchiveTab.Content = $"Archive  {notes.Count(n => n.State == NoteState.Archived)}";

        var items = notes
            .Where(n => (n.State == NoteState.Archived) == ShowingArchive && NoteSearch.Matches(n, query))
            .OrderByDescending(n => n.ModifiedUtc)
            .Select(n => new NoteItem(n))
            .ToList();
        List.ItemsSource = items;
        List.SelectedItem = items.FirstOrDefault(i => i.Note.Id == selectedId);

        EmptyText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyText.Text = !string.IsNullOrWhiteSpace(query) ? "No notes match your search."
            : ShowingArchive ? "Archived notes show up here.\nArchive a note with Ctrl+D."
            : $"No notes yet.\nPress {_manager.Settings.NewNoteHotkey} anywhere to create one.";
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control)
        {
            _manager.CreateNote();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            if (SearchBox.Text.Length > 0)
                SearchBox.Clear();
            else
                Close();
            e.Handled = true;
        }
        else if (e.Key == Key.Down && SearchBox.IsKeyboardFocusWithin && List.Items.Count > 0)
        {
            List.SelectedIndex = Math.Max(0, List.SelectedIndex);
            (List.ItemContainerGenerator.ContainerFromIndex(List.SelectedIndex) as ListBoxItem)?.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && SearchBox.IsKeyboardFocusWithin && List.Items.Count > 0)
        {
            Open((NoteItem)List.Items[0]);
            e.Handled = true;
        }
        base.OnPreviewKeyDown(e);
    }

    void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SearchHint.Visibility = SearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        Refresh();
    }

    void Tab_Checked(object sender, RoutedEventArgs e)
    {
        if (IsLoaded)
            Refresh();
    }

    void NewNote_Click(object sender, RoutedEventArgs e) => _manager.CreateNote();

    void List_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (List.SelectedItem is NoteItem item && e.OriginalSource is not Button)
            Open(item);
    }

    void List_KeyDown(object sender, KeyEventArgs e)
    {
        if (List.SelectedItem is not NoteItem item)
            return;
        if (e.Key == Key.Enter)
            Open(item);
        else if (e.Key == Key.Delete)
        {
            if (item.Note.State == NoteState.Archived)
                ConfirmDelete(item.Note);
            else
                _manager.ArchiveNote(item.Note);
        }
        else
            return;
        e.Handled = true;
    }

    void Open(NoteItem item) => _manager.ShowNote(item.Note);

    static NoteItem ItemOf(object sender) => (NoteItem)((FrameworkElement)sender).DataContext;

    void Archive_Click(object sender, RoutedEventArgs e) => _manager.ArchiveNote(ItemOf(sender).Note);
    void Restore_Click(object sender, RoutedEventArgs e) => _manager.ShowNote(ItemOf(sender).Note);
    void Delete_Click(object sender, RoutedEventArgs e) => ConfirmDelete(ItemOf(sender).Note);

    void ConfirmDelete(Note note)
    {
        var title = note.Title.Length > 40 ? note.Title[..40] + "…" : note.Title;
        var answer = MessageBox.Show(this, $"Delete \"{title}\" permanently?\nThis can't be undone.",
            "Open Note", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (answer == MessageBoxResult.Yes)
            _manager.DeleteForever(note);
    }
}

/// <summary>Display data for one row of the list.</summary>
internal sealed class NoteItem(Note note)
{
    public Note Note { get; } = note;
    public string Title => Note.IsEmpty ? "(empty note)" : Note.Title;
    public string Preview => Note.Preview;
    public Visibility PreviewVisibility => Preview.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

    public string Meta
    {
        get
        {
            var parts = new List<string>();
            if (Note.State == NoteState.Hidden) parts.Add("closed");
            if (Note.IsLocked) parts.Add("locked");
            parts.Add(Note.ModifiedUtc.ToLocalTime().ToString("d MMM yyyy, HH:mm", CultureInfo.CurrentCulture));
            var meta = string.Join(", ", parts);
            return char.ToUpper(meta[0]) + meta[1..];
        }
    }

    public Brush Swatch => Solid(Palette.ParseRgb(Note.Color));
    public Brush StripSwatch => Solid(Palette.Strip(Note.Color));

    static SolidColorBrush Solid((byte R, byte G, byte B) c)
    {
        var brush = new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B));
        brush.Freeze();
        return brush;
    }

    public Visibility ActiveVisibility => Note.State != NoteState.Archived ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ArchivedVisibility => Note.State == NoteState.Archived ? Visibility.Visible : Visibility.Collapsed;
}
