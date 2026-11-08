using System.IO;
using System.Windows.Threading;
using OpenNote.Core;
using OpenNote.Views;

namespace OpenNote.Services;

/// <summary>
/// Single owner of all notes and their windows. Windows report edits here; this class
/// decides what a note's state is and saves (debounced) after every change.
/// </summary>
internal sealed class NoteManager
{
    static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(500);
    static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);

    readonly NoteStore _store;
    readonly StoreData _data;
    readonly Dictionary<Guid, NoteWindow> _windows = [];
    readonly DispatcherTimer _saveTimer;
    bool _dirty, _saveFailureReported;
    string _lastSaveError = "";
    PickNotesWindow? _picker;
    Guid? _lastActive;

    /// <summary>Raised after any change that a list of notes would show (text, state, color...).</summary>
    public event Action? NotesChanged;

    /// <summary>Raised once per streak of failed saves, with the reason.</summary>
    public event Action<string>? SaveFailed;

    public NoteManager(NoteStore store, AppSettings settings)
    {
        _store = store;
        Settings = settings;
        _data = store.Load();
        Images = new ImageStore(store.Directory);
        if (store.LoadedCleanly)
            Images.DeleteUnreferenced(_data.Notes.Select(n => n.Text), store.OtherNoteFiles());
        _saveTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = SaveDelay };
        _saveTimer.Tick += (_, _) => SaveNow();
    }

    public AppSettings Settings { get; }
    public ImageStore Images { get; }
    public IReadOnlyList<Note> Notes => _data.Notes;
    public bool IsShuttingDown { get; private set; }

    public void RestoreWindows()
    {
        foreach (var note in _data.Notes.Where(n => n.State == NoteState.Visible).ToList())
            OpenWindow(note, activate: false);
    }

    /// <summary>The first note, in the display language: a few things to try.</summary>
    public void CreateWelcomeNote() => CreateNote(text: WelcomeText(), heading: Ui.T("Welcome to Open Note"));

    /// <summary>
    /// The current "what's new" note. Change it (and <see cref="WhatsNewText"/>) for a release
    /// that should tell people what changed; settings.json remembers the last one shown.
    /// </summary>
    public const string WhatsNewId = "flower";

    /// <summary>Once after an update: what changed, in a bigger note in the middle of the screen.</summary>
    public void CreateWhatsNewNote() =>
        CreateNote(text: WhatsNewText(), heading: Ui.T("Open Note has been updated"), activate: false, centered: new System.Windows.Size(400, 440));

    /// <summary>After your languages change: spell check (and its dictionary) follow them in every open note.</summary>
    public void ApplyLanguages()
    {
        foreach (var w in _windows.Values)
            w.ApplyLanguages();
    }

    /// <summary>Fades every open note to 10% (another app is in use), or brings them back.</summary>
    public void SetFaded(bool faded)
    {
        foreach (var w in _windows.Values)
            w.SetFaded(faded);
    }

    /// <summary>True on the very first start (no notes file yet).</summary>
    public bool IsFirstRun => _store.IsFirstRun;

    /// <param name="activate">False for a note an AI agent writes: it shows up without taking the keyboard from the user.</param>
    /// <param name="centered">Opens it in the middle of the main screen at this size (in DIPs), not at the mouse.</param>
    public Note CreateNote(NoteWindow? near = null, string text = "", string heading = "", string? color = null, bool activate = true,
        System.Windows.Size? centered = null)
    {
        var note = new Note
        {
            Heading = heading,
            Text = text,
            Color = color ?? Settings.DefaultColor,
            Opacity = Settings.DefaultOpacity,
            FontSize = Settings.DefaultFontSize,
            Font = Settings.DefaultFont,
        };
        _data.Notes.Add(note);

        // Next to the note it was created from, else where the mouse is (so the note lands on the monitor you're using).
        var spawn = near is not null
            ? new PixelPoint(near.Note.X + 32, near.Note.Y + 32)
            : Offset(Native.Win32.CursorPosition(), -60, -12);
        OpenWindow(note, activate, spawn, centered);
        Changed();
        return note;
    }

    /// <summary>
    /// Opens an .onote file as a new note (its pictures are copied into this data folder), at the
    /// mouse, in front. Throws <see cref="InvalidDataException"/> for a file that isn't one.
    /// </summary>
    public Note ImportNote(byte[] file)
    {
        var note = NoteFile.FromOnote(file, Images.Save);
        var at = Offset(Native.Win32.CursorPosition(), -60, -12);
        if (note.Width > 0 && note.Height > 0)
            (note.X, note.Y) = (at.X, at.Y); // its saved size, brought fully on screen when it opens
        _data.Notes.Add(note);
        OpenWindow(note, activate: true, at);
        Changed();
        return note;
    }

    /// <summary>Adds lines at the end of a note; an open note shows them right away.</summary>
    public void AppendText(Note note, string text)
    {
        note.Text = note.Text.Trim('\r', '\n').Length == 0 ? text : note.Text.TrimEnd('\r', '\n') + "\r\n" + text;
        if (_windows.TryGetValue(note.Id, out var window))
            window.ReloadText();
        NoteEdited(note);
    }

    /// <summary>Shows a note (restoring it from hidden or archived) and focuses it.</summary>
    public void ShowNote(Note note)
    {
        note.State = NoteState.Visible;
        OpenWindow(note, activate: true);
        Changed();
    }

    /// <summary>
    /// Closes a note's window (the x button). The note stays in the list, not in the Archive;
    /// an empty note is simply discarded.
    /// </summary>
    public void CloseNote(Note note)
    {
        CommitTitle(note);
        if (note.IsEmpty)
        {
            DeleteForever(note);
            return;
        }
        note.State = NoteState.Hidden;
        CloseWindow(note);
        Changed();
    }

    public void ArchiveNote(Note note)
    {
        CommitTitle(note);
        if (note.IsEmpty)
        {
            DeleteForever(note);
            return;
        }
        note.State = NoteState.Archived;
        CloseWindow(note);
        Changed();
    }

    /// <summary>A title being typed (F2) is kept: the × and the menu don't take focus, so it isn't committed by itself.</summary>
    void CommitTitle(Note note)
    {
        if (_windows.TryGetValue(note.Id, out var window))
            window.CommitTitleEdit();
    }

    public void DeleteForever(Note note)
    {
        CloseWindow(note);
        _data.Notes.Remove(note);
        _data.FocusSet.Remove(note.Id);
        Changed();
    }

    /// <summary>Shows every visible-state note above other apps. Returns false when there are none.</summary>
    public bool BringAllToFront()
    {
        var visible = _data.Notes.Where(n => n.State == NoteState.Visible).ToList();
        if (visible.Count == 0)
            return false;

        foreach (var note in visible)
            OpenWindow(note, activate: false);
        foreach (var note in visible)
            _windows[note.Id].BringToFront();

        var focus = _lastActive is { } id && _windows.TryGetValue(id, out var last) ? last : _windows[visible[^1].Id];
        focus.ActivateAndFocus();
        return true;
    }

    /// <summary>Temporarily hides all note windows (they come back on the next show-all or restart).</summary>
    public void HideAll()
    {
        foreach (var w in _windows.Values)
            w.Hide();
        App.Instance.TrimSoon(TimeSpan.FromSeconds(2));
    }

    List<Note> OpenNotes() => _data.Notes.Where(n => n.State == NoteState.Visible).ToList();

    HashSet<Guid> ShowingIds() =>
        OpenNotes().Where(n => _windows.TryGetValue(n.Id, out var w) && w.IsVisible).Select(n => n.Id).ToHashSet();

    /// <summary>The saved selection, without notes that were hidden, archived or deleted since.</summary>
    HashSet<Guid> SavedSelection()
    {
        var open = OpenNotes().Select(n => n.Id).ToHashSet();
        return _data.FocusSet.Where(open.Contains).ToHashSet();
    }

    /// <summary>True when the note is in the saved selection (the pin button; the Ctrl+Alt+P list).</summary>
    public bool IsInSelection(Note note) => _data.FocusSet.Contains(note.Id);

    /// <summary>The pin button: adds the note to the saved selection, or takes it out.</summary>
    public void ToggleInSelection(Note note)
    {
        if (!_data.FocusSet.Remove(note.Id))
            _data.FocusSet.Add(note.Id);
        Changed();
        RefreshPins();
    }

    void RefreshPins()
    {
        foreach (var w in _windows.Values)
            w.RefreshChrome();
    }

    /// <summary>
    /// The master shortcut: hides every note when all are showing; otherwise shows them all,
    /// which also brings back the rest while only the saved selection is showing.
    /// </summary>
    /// <returns>False when there are no notes on the desktop to show or hide.</returns>
    public bool ToggleAll()
    {
        var showing = ShowingIds();
        var open = OpenNotes();
        if (open.Count == 0)
            return false;
        if (NoteVisibility.ShouldHideAll(open.Select(n => showing.Contains(n.Id)).ToList()))
            HideAll();
        else
            BringAllToFront();
        return true;
    }

    /// <summary>Shows only the saved selection, or hides everything if exactly that is already showing.</summary>
    public void ToggleSavedSelection()
    {
        var saved = SavedSelection();
        switch (NoteVisibility.ForFocusShortcut(ShowingIds(), saved))
        {
            case FocusAction.Pick:
                PickSelection();
                break;
            case FocusAction.HideAll:
                HideAll();
                break;
            default:
                ShowOnly(saved);
                break;
        }
    }

    void ShowOnly(HashSet<Guid> keep)
    {
        foreach (var note in OpenNotes())
        {
            if (keep.Contains(note.Id))
            {
                OpenWindow(note, activate: false);
                _windows[note.Id].BringToFront();
            }
            else if (_windows.TryGetValue(note.Id, out var window))
            {
                window.Hide(); // hidden only for now; the master shortcut brings it back
            }
        }
        if (OpenNotes().FirstOrDefault(n => keep.Contains(n.Id)) is { } first)
            _windows[first.Id].ActivateAndFocus();
        App.Instance.TrimSoon(TimeSpan.FromSeconds(2));
    }

    /// <summary>
    /// Shows every note and asks which ones to keep. Enter, or the same shortcut again, saves
    /// that choice as the (single) saved selection, hides the others and closes the picker.
    /// </summary>
    public void PickSelection()
    {
        if (_picker is not null)
        {
            _picker.Confirm();
            return;
        }
        if (!BringAllToFront())
            return;

        var saved = SavedSelection();
        var open = OpenNotes();
        var preselected = saved.Count > 0 ? saved : open.Select(n => n.Id).ToHashSet();
        var picker = new PickNotesWindow(open, preselected);
        picker.Confirmed += chosen =>
        {
            var keep = chosen.Select(n => n.Id).ToHashSet();
            _data.FocusSet = keep.ToList();
            Changed();
            RefreshPins();
            ShowOnly(keep);
        };
        picker.Closed += (_, _) => _picker = null;
        _picker = picker;
        picker.Show();
    }

    public void NoteEdited(Note note)
    {
        note.ModifiedUtc = DateTime.UtcNow;
        Changed();
    }

    /// <summary>Position/size/appearance changes: saved, but nothing a list needs to redraw for.</summary>
    public void NoteMoved() => ScheduleSave();

    /// <summary>Applies the "keep notes on top" setting to every open note.</summary>
    public void RefreshTopmost()
    {
        foreach (var w in _windows.Values)
            w.ApplyTopmost();
    }

    public void NoteActivated(Note note) => _lastActive = note.Id;

    public void Flush()
    {
        if (_dirty)
            SaveNow();
    }

    /// <summary>Saves now; false (with the reason) when the notes couldn't be written.</summary>
    public bool TrySave(out string reason)
    {
        Flush();
        reason = _lastSaveError;
        return !_dirty;
    }

    public void Shutdown()
    {
        IsShuttingDown = true;
        Flush();
        foreach (var w in _windows.Values.ToList())
            w.CloseFromManager();
        _windows.Clear();
        Flush(); // a title being renamed is only committed as its window closes
    }

    void OpenWindow(Note note, bool activate, PixelPoint? spawnAt = null, System.Windows.Size? centered = null)
    {
        if (!_windows.TryGetValue(note.Id, out var window))
        {
            window = new NoteWindow(this, note, spawnAt) { ShowActivated = activate, CenteredSize = centered };
            _windows[note.Id] = window;
        }

        if (!window.IsVisible)
            window.Show();
        if (activate)
        {
            window.BringToFront();
            window.ActivateAndFocus();
        }
    }

    void CloseWindow(Note note)
    {
        if (_windows.Remove(note.Id, out var window))
            window.CloseFromManager();
    }

    void Changed()
    {
        ScheduleSave();
        NotesChanged?.Invoke();
    }

    void ScheduleSave()
    {
        _dirty = true;
        _saveTimer.Stop();
        _saveTimer.Interval = SaveDelay;
        _saveTimer.Start();
    }

    void SaveNow()
    {
        _saveTimer.Stop();
        try
        {
            _store.Save(_data);
            _dirty = false;
            _saveFailureReported = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _lastSaveError = ex.Message;
            // Keep the data in memory and retry; tell the user once rather than on every keystroke.
            if (!_saveFailureReported)
                SaveFailed?.Invoke(ex.Message);
            _saveFailureReported = true;
            _saveTimer.Interval = RetryDelay;
            _saveTimer.Start();
        }
    }

    static PixelPoint Offset(PixelPoint p, int dx, int dy) => new(p.X + dx, p.Y + dy);

    string WhatsNewText() =>
        $"""
        {Ui.T("This is the flower version. What's new:")}
        • {Ui.T("AI agents (Claude Code, Codex, Cursor…) can now read your notes and write new ones for you: {0} → AI agents", Settings.HelpHotkey)}
        • {Ui.T("Text color: select text, then Ctrl+H or right-click → Text color")}
        • {Ui.T("Save a note as a file (⋯ → Save as file…): .txt for just the text, or .onote to open it again in Open Note with its colors and pictures, on any PC")}
        • {Ui.T("Flowers: click the ☆ of a note to mark it with a flower")}
        • {Ui.T("A new look for the flower version, and a little letter hidden in {0}", Settings.HelpHotkey)}

        {Ui.T("You can delete this note: it won't come back.")}
        """.Replace("\n", "\r\n").Replace("\r\r\n", "\r\n");

    string WelcomeText() =>
        $"""
        ☐ {Ui.T("Drag the header to move me")}
        ☐ {Ui.T("Click the title to rename me")}
        ☐ {Ui.T("Type /> for a box, /- for a bullet")}
        ☐ {Ui.T("Right-click the header for colors")}

        {Settings.NewNoteHotkey}  {Ui.T("new note")}
        {Settings.ToggleAllHotkey}  {Ui.T("show or hide notes")}
        {Settings.NotesListHotkey}  {Ui.T("all notes")}
        """.Replace("\n", "\r\n").Replace("\r\r\n", "\r\n");
}
