using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using OpenNote.Native;

namespace OpenNote.Services;

/// <summary>The app's only permanent presence: a notification-area icon and its menu.</summary>
internal sealed class TrayService : IDisposable
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool SetForegroundWindow(nint hwnd);

    readonly TrayIcon _icon = new("Open Note");
    readonly App _app;
    readonly NoteManager _notes;
    readonly string _dataDirectory;

    public TrayService(App app, NoteManager notes, string dataDirectory)
    {
        _app = app;
        _notes = notes;
        _dataDirectory = dataDirectory;
        _icon.LeftClick += app.ShowAllNotes;
        _icon.RightClick += ShowMenu;
    }

    public void ShowWarning(string title, string text) => _icon.ShowWarning(title, text);

    /// <summary>Built on demand and dropped when closed, so it costs nothing while unused.</summary>
    void ShowMenu()
    {
        var s = _notes.Settings;
        var menu = new ContextMenu { Placement = PlacementMode.MousePoint };
        menu.Items.Add(Item("New note", s.NewNoteHotkey, () => _notes.CreateNote()));
        menu.Items.Add(Item("Show all notes", s.ToggleAllHotkey, _app.ShowAllNotes));
        menu.Items.Add(Item("Hide all notes", "", _notes.HideAll));
        menu.Items.Add(Item("Choose notes to keep…", s.PickNotesHotkey, _notes.PickSelection));
        menu.Items.Add(Item("Show saved selection", s.SavedNotesHotkey, _notes.ToggleSavedSelection));
        menu.Items.Add(Item("All notes and archive…", s.NotesListHotkey, _app.ShowNotesList));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Keep notes on top", "", () =>
        {
            bool onTop = !s.KeepNotesOnTop;
            var error = s.Update(_dataDirectory, x => x.KeepNotesOnTop = onTop);
            _notes.RefreshTopmost();
            if (error is not null)
                ShowWarning("Couldn't save the setting", error);
        }, isChecked: s.KeepNotesOnTop));
        menu.Items.Add(Item("Gemini API keys…", s.ApiKeysHotkey, _app.ShowApiKeys));
        if (Packaging.IsPackaged)
            // A Store package starts with Windows through its manifest; Windows' own page turns it on or off.
            menu.Items.Add(Item("Start with Windows…", "", () => OpenFile("ms-settings:startupapps")));
        else
            menu.Items.Add(Item("Start with Windows", "", () => StartupRegistration.SetEnabled(!StartupRegistration.IsEnabled),
                isChecked: StartupRegistration.IsEnabled));
        menu.Items.Add(Item("Edit settings…", "", () => OpenFile(Core.AppSettings.PathIn(_dataDirectory))));
        menu.Items.Add(Item("Reload settings", "", _app.ReloadSettings));
        menu.Items.Add(Item("Open data folder", "", () => Process.Start("explorer.exe", $"\"{RealDataDirectory()}\"")));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Exit", "", _app.ExitApp));

        // Classic tray-menu rule: the menu must belong to the foreground window, otherwise it
        // won't close when you click elsewhere.
        menu.Opened += (_, _) =>
        {
            if (PresentationSource.FromVisual(menu) is HwndSource source)
                SetForegroundWindow(source.Handle);
        };
        SetForegroundWindow(_icon.Handle);
        menu.IsOpen = true;
    }

    /// <summary>The data folder as Explorer sees it (a Store package's app data is redirected).</summary>
    string RealDataDirectory() =>
        Packaging.IsPackaged && _dataDirectory == Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenNote")
            ? Packaging.RealLocalAppDataPath("OpenNote")
            : _dataDirectory;

    static MenuItem Item(string text, string shortcut, Action action, bool isChecked = false)
    {
        var item = new MenuItem { Header = text, InputGestureText = shortcut, IsChecked = isChecked };
        item.Click += (_, _) => action();
        return item;
    }

    static void OpenFile(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // No app associated with .json: fall back to Notepad.
            Process.Start("notepad.exe", $"\"{path}\"");
        }
    }

    public void Dispose() => _icon.Dispose();
}
