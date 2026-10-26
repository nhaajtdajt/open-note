using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using OpenNote.Core;
using OpenNote.Native;
using OpenNote.Services;
using OpenNote.Views;

namespace OpenNote;

public partial class App : Application
{
    Mutex? _instanceMutex;
    readonly List<(EventWaitHandle Signal, RegisteredWaitHandle Wait)> _signals = [];
    NoteManager? _notes;
    HotkeyService? _hotkeys;
    TrayService? _tray;
    NotesListWindow? _notesList;
    HelpWindow? _help;
    ApiKeysWindow? _apiKeys;
    DispatcherTimer? _trimTimer;
    string _dataDirectory = "";
    bool _customDataDirectory;

    internal static App Instance => (App)Current;

    /// <summary>Offline read-aloud (Windows voices); the speech engine is only created while reading.</summary>
    internal SpeechService Speech { get; } = new();

    /// <summary>AI text checking; built from settings.json (and rebuilt on Reload settings).</summary>
    internal AiCorrector Ai { get; private set; } = new([]);

    /// <summary>How long the helper left behind at shutdown waits before bringing OpenNote back.</summary>
    static readonly TimeSpan ResumeDelay = TimeSpan.FromSeconds(20);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var custom = ParseDataDirectory(e.Args);
        _customDataDirectory = custom is not null;
        _dataDirectory = custom ?? DefaultDataDirectory();

        if (e.Args.Contains("--resume"))
        {
            // Started as Windows began ending the session (see OnSessionEnding). If the PC really
            // shuts down, this window-less process simply ends with it; if the shutdown was
            // cancelled, it carries on after a pause and becomes OpenNote again.
            var wait = new DispatcherTimer { Interval = ResumeDelay };
            wait.Tick += (_, _) =>
            {
                wait.Stop();
                Start(e.Args, resumed: true);
            };
            wait.Start();
            return;
        }
        Start(e.Args, resumed: false);
    }

    void Start(string[] args, bool resumed)
    {
        // One instance per data folder. Launching again asks the running instance for a new
        // note; "--exit" asks it to save and quit (used when updating the app).
        var id = InstanceId(_dataDirectory);
        var newNoteName = $@"Local\OpenNote-new-{id}";
        var exitName = $@"Local\OpenNote-exit-{id}";
        _instanceMutex = new Mutex(initiallyOwned: true, $@"Local\OpenNote-{id}", out bool isFirst);
        if (!isFirst || args.Contains("--exit"))
        {
            // A resume helper that finds OpenNote already running (e.g. started at sign-in) just leaves quietly.
            bool exit = args.Contains("--exit");
            if (!resumed && EventWaitHandle.TryOpenExisting(exit ? exitName : newNoteName, out var signal))
            {
                signal.Set();
                signal.Dispose();
                if (exit && !isFirst)
                    isFirst = WaitForExit(_instanceMutex); // an installer replaces the files next
            }
            if (isFirst)
                _instanceMutex.ReleaseMutex();
            _instanceMutex.Dispose();
            _instanceMutex = null;
            Shutdown();
            return;
        }
        Listen(newNoteName, () => _notes?.CreateNote());
        Listen(exitName, ExitApp);

        // Notes are small per-pixel-transparent (layered) windows. With GPU rendering WPF keeps
        // Direct3D surfaces per window and copies them back for UpdateLayeredWindow; measured with
        // 6 notes that's ~153 MB private vs ~68 MB in software mode, with no idle CPU either way.
        System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;

        DispatcherUnhandledException += OnUnhandledException;
        SessionEnding += OnSessionEnding;
        // When you go back to other apps, give idle memory back to Windows.
        Deactivated += (_, _) => TrimSoon(TimeSpan.FromSeconds(10));
        Activated += (_, _) => _trimTimer?.Stop();
        // "Fade when inactive": notes fade while another app is in use and come back as soon as
        // Open Note is (a note is clicked, or any of its windows opens).
        Deactivated += (_, _) =>
        {
            if (_notes?.Settings.FadeWhenInactive == true)
                _notes.SetFaded(true);
        };
        Activated += (_, _) => _notes?.SetFaded(false);

        try
        {
            var settings = AppSettings.Load(_dataDirectory, out var settingsError);
            _notes = new NoteManager(new NoteStore(_dataDirectory), settings);
            if (_notes.IsFirstRun && settings.UiLanguage.Length == 0)
                PickLanguage(settings);
            Ui.Use(settings.DisplayLanguage());
            Ai = settings.CreateAiCorrector();
            _notes.SaveFailed += reason => _tray?.ShowWarning(Ui.T("Open Note couldn't save"), reason + "\n" + Ui.T("Your notes are kept in memory and saving will be retried."));
            _tray = new TrayService(this, _notes, _dataDirectory);
            _hotkeys = new HotkeyService();

            _notes.RestoreWindows();
            if (settings.FadeWhenInactive)
                _notes.SetFaded(true); // starting in the background, with another app in front
            ConfigureStartWithWindows(settings);
            RegisterHotkeys(settingsError);
            // Someone who just installed Open Note sees how to use it before anything else.
            if (_notes.IsFirstRun)
            {
                _notes.CreateWelcomeNote();
                Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, ToggleHelp);
            }
        }
        catch (Exception ex)
        {
            // Without this the error handler below would keep a process running with no tray icon,
            // holding the single-instance lock, so every later launch would silently do nothing.
            LogError(ex);
            MessageBox.Show($"Open Note couldn't start:\n{ex.Message}\n\nYour notes are untouched in {_dataDirectory}.",
                "Open Note", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }
        TrimSoon(TimeSpan.FromSeconds(3));
    }

    /// <summary>
    /// WPF quits as soon as Windows *asks* whether the session may end, even if another app then
    /// blocks the shutdown and the PC just goes to sleep instead. Save, and leave a window-less
    /// helper ("--resume") behind that restarts OpenNote if the session turns out not to end.
    /// </summary>
    void OnSessionEnding(object? sender, SessionEndingCancelEventArgs e)
    {
        _notes?.Shutdown();
        if (Environment.ProcessPath is not { } exe)
            return;
        try
        {
            var start = new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = false };
            start.ArgumentList.Add("--resume");
            start.ArgumentList.Add("--data");
            start.ArgumentList.Add(_dataDirectory);
            System.Diagnostics.Process.Start(start)?.Dispose();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // Windows may refuse new processes this late in a real shutdown; nothing to bring back then.
        }
    }

    void Listen(string name, Action action)
    {
        var signal = new EventWaitHandle(false, EventResetMode.AutoReset, name);
        var wait = ThreadPool.RegisterWaitForSingleObject(signal,
            (_, _) => Dispatcher.BeginInvoke(action), null, Timeout.Infinite, executeOnlyOnce: false);
        _signals.Add((signal, wait));
    }

    void RegisterHotkeys(string? extraWarning = null)
    {
        var s = _notes!.Settings;
        var problems = new[]
        {
            _hotkeys!.Register(s.NewNoteHotkey, () => _notes.CreateNote()),
            _hotkeys.Register(s.ToggleAllHotkey, () =>
            {
                // Nothing on the desktop (every note is closed or archived): say so, rather than open
                // the notes list, which belongs to its own shortcut.
                if (!_notes.ToggleAll())
                    _tray!.ShowInfo(Ui.T("No notes on the desktop"), $"{s.NewNoteHotkey}: {Ui.T("new note")}\n{s.NotesListHotkey}: {Ui.T("all notes and archive")}");
            }),
            _hotkeys.Register(s.NotesListHotkey, ToggleNotesList),
            _hotkeys.Register(s.ApiKeysHotkey, ShowApiKeys),
            _hotkeys.Register(s.PickNotesHotkey, _notes.PickSelection),
            _hotkeys.Register(s.SavedNotesHotkey, _notes.ToggleSavedSelection),
            _hotkeys.Register(s.HelpHotkey, ToggleHelp),
            _hotkeys.Register(s.QuitHotkey, ExitApp),
            _hotkeys.Register(s.FadeHotkey, ToggleFadeWhenInactive),
        }.Where(p => p is not null).ToList();
        if (extraWarning is not null)
            problems.Insert(0, extraWarning);

        if (problems.Count > 0)
            _tray!.ShowWarning(Ui.T("Some shortcuts are unavailable"), string.Join("\n", problems) + "\n" + Ui.T("Change them via the tray icon → Edit settings."));
    }

    /// <summary>The fade shortcut / tray item: turns "fade when inactive" on or off and shows the result right away.</summary>
    internal void ToggleFadeWhenInactive()
    {
        var s = _notes!.Settings;
        bool on = !s.FadeWhenInactive;
        var error = s.Update(_dataDirectory, x => x.FadeWhenInactive = on);
        // The shortcut is usually pressed while another app is in front.
        _notes.SetFaded(on && !Windows.OfType<Window>().Any(w => w.IsActive));
        if (error is not null)
            _tray!.ShowWarning(Ui.T("Couldn't save the setting"), error);
        else
            _tray!.ShowInfo(on ? Ui.T("Notes fade while you use other apps") : Ui.T("Notes stay solid"),
                on ? Ui.T("Click a note to bring them back. {0} turns this off.", s.FadeHotkey) : Ui.T("{0} turns fading back on.", s.FadeHotkey));
    }

    internal void ShowAllNotes()
    {
        if (!_notes!.BringAllToFront())
            ShowNotesList();
    }

    /// <summary>
    /// The list shortcut: opens the list, and closes it when it is the window in front. A list
    /// left open behind other windows is brought to the front instead of being closed unseen.
    /// </summary>
    internal void ToggleNotesList()
    {
        if (_notesList is { IsVisible: true, IsActive: true })
            _notesList.Close();
        else
            ShowNotesList();
    }

    /// <summary>
    /// Ready to go without any setup. The first run turns on "Start with Windows" and puts Open
    /// Note in the Start menu (so it can be found by name); every run keeps both pointing at the
    /// current exe. A Store package gets both from its manifest instead.
    /// </summary>
    void ConfigureStartWithWindows(AppSettings settings)
    {
        // A copy run on another data folder (--data, e.g. a portable or test copy) must not take
        // over the main copy's entries.
        if (Packaging.IsPackaged || _customDataDirectory)
            return;
        try
        {
            if (settings.ReadFailed)
            {
                // Can't tell what the user turned off: leave everything as it is.
                return;
            }

            if (!settings.AutoStartConfigured)
            {
                if (settings.Update(_dataDirectory, x => x.AutoStartConfigured = true) is null)
                    StartupRegistration.SetEnabled(true); // only once it's remembered, so turning it off sticks
            }
            else
            {
                StartupRegistration.Refresh();
            }

            // Writing a shortcut wakes up the Windows shell (handles, threads, memory), so it is only
            // done on the first run and when the exe has moved, not at every start.
            var exe = Environment.ProcessPath ?? "";
            bool moved = !string.Equals(settings.StartMenuShortcutTarget, exe, StringComparison.OrdinalIgnoreCase);
            if (!settings.StartMenuConfigured || (moved && StartMenuShortcut.Exists))
            {
                if (StartMenuShortcut.Write())
                    settings.Update(_dataDirectory, x => (x.StartMenuConfigured, x.StartMenuShortcutTarget) = (true, exe));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // Not fatal: the app still runs, it just won't start by itself.
        }
    }

    /// <summary>Opens the Gemini API keys window (one at a time).</summary>
    internal void ShowApiKeys()
    {
        if (_apiKeys is null)
        {
            var s = _notes!.Settings;
            _apiKeys = new ApiKeysWindow(s.GeminiApiKeys, SaveGeminiKeys, s.Native(), s.Learning(), SaveLanguages);
            _apiKeys.Closed += (_, _) =>
            {
                _apiKeys = null;
                TrimSoon(TimeSpan.FromSeconds(2));
            };
        }
        _apiKeys.Show();
        _apiKeys.Activate();
    }

    /// <summary>
    /// The very first start: which language Open Note speaks. A language other than English is
    /// also taken as your own language for Check with AI (someone picking English may well be
    /// learning it, so then that is still guessed from Windows).
    /// </summary>
    void PickLanguage(AppSettings settings)
    {
        var picker = new LanguagePickWindow(settings.DisplayLanguage());
        picker.ShowDialog();
        var chosen = picker.Chosen;
        settings.Update(_dataDirectory, x =>
        {
            x.UiLanguage = chosen.Code;
            if (x.NativeLanguage.Length == 0 && chosen != Language.English)
                x.NativeLanguage = chosen.Code;
        });
    }

    /// <summary>Changes the display language (from the how-to card); windows opened from now on use it.</summary>
    internal void SetUiLanguage(Language language)
    {
        var error = _notes!.Settings.Update(_dataDirectory, x => x.UiLanguage = language.Code);
        Ui.Use(language);
        if (error is not null)
            _tray!.ShowWarning(Ui.T("Couldn't save the setting"), error);
    }

    /// <summary>Saves your language and the one you're learning; Check with AI, spell check and read aloud follow them at once.</summary>
    void SaveLanguages(Language native, Language learning)
    {
        var s = _notes!.Settings;
        var error = s.Update(_dataDirectory, x => (x.NativeLanguage, x.LearningLanguage) = (native.Code, learning.Code));
        Ai = s.CreateAiCorrector();
        _notes.ApplyLanguages();
        if (error is not null)
            _tray!.ShowWarning(Ui.T("Couldn't save the languages"), error);
    }

    /// <summary>Saves checked keys to settings.json and starts using them right away.</summary>
    string? SaveGeminiKeys(IReadOnlyList<string> keys)
    {
        var s = _notes!.Settings;
        var error = s.Update(_dataDirectory, x => x.GeminiApiKeys = [.. keys]);
        Ai = s.CreateAiCorrector(); // usable now even if the file couldn't be written
        return error;
    }

    internal void ShowNotesList()
    {
        if (_notesList is null)
        {
            _notesList = new NotesListWindow(_notes!);
            _notesList.Closed += (_, _) =>
            {
                _notesList = null;
                TrimSoon(TimeSpan.FromSeconds(2));
            };
        }
        if (_notesList.WindowState == WindowState.Minimized)
            _notesList.WindowState = WindowState.Normal;
        _notesList.Show();
        _notesList.Activate();
        _notesList.SearchBox.Focus();
    }

    /// <summary>Re-reads settings.json and re-registers the global shortcuts.</summary>
    internal void ReloadSettings()
    {
        var fresh = AppSettings.Load(_dataDirectory, out var error);
        var s = _notes!.Settings;
        (s.NewNoteHotkey, s.ToggleAllHotkey, s.NotesListHotkey) =
            (fresh.NewNoteHotkey, fresh.ToggleAllHotkey, fresh.NotesListHotkey);
        (s.DefaultColor, s.DefaultOpacity, s.DefaultFontSize, s.DefaultFont) =
            (fresh.DefaultColor, fresh.DefaultOpacity, fresh.DefaultFontSize, fresh.DefaultFont);
        (s.GeminiApiKeys, s.GeminiModels) = (fresh.GeminiApiKeys, fresh.GeminiModels);
        (s.OpenCodeApiKey, s.OpenCodeModels, s.AiServices) = (fresh.OpenCodeApiKey, fresh.OpenCodeModels, fresh.AiServices);
        (s.PickNotesHotkey, s.SavedNotesHotkey) = (fresh.PickNotesHotkey, fresh.SavedNotesHotkey);
        (s.KeepNotesOnTop, s.NoteEffects, s.UiLanguage, s.HelpLanguage) = (fresh.KeepNotesOnTop, fresh.NoteEffects, fresh.UiLanguage, fresh.HelpLanguage);
        Ui.Use(s.DisplayLanguage());
        (s.FadeWhenInactive, s.FadeHotkey) = (fresh.FadeWhenInactive, fresh.FadeHotkey);
        (s.NativeLanguage, s.LearningLanguage) = (fresh.NativeLanguage, fresh.LearningLanguage);
        if (!s.FadeWhenInactive)
            _notes.SetFaded(false);
        (s.ApiKeysHotkey, s.HelpHotkey, s.QuitHotkey) = (fresh.ApiKeysHotkey, fresh.HelpHotkey, fresh.QuitHotkey);
        _notes.RefreshTopmost();
        _notes.ApplyLanguages();
        Ai = s.CreateAiCorrector();
        _hotkeys!.UnregisterAll();
        RegisterHotkeys(error);
    }

    /// <summary>Trims memory once the app has been left alone for <paramref name="delay"/>.</summary>
    internal void TrimSoon(TimeSpan delay)
    {
        if (_trimTimer is null)
        {
            _trimTimer = new DispatcherTimer(DispatcherPriority.ApplicationIdle);
            _trimTimer.Tick += (_, _) =>
            {
                _trimTimer.Stop();
                Memory.Trim();
            };
        }
        _trimTimer.Stop();
        _trimTimer.Interval = delay;
        _trimTimer.Start();
    }

    /// <summary>The guide card in the middle of the screen; the shortcut again closes it.</summary>
    internal void ToggleHelp()
    {
        if (_help is not null)
        {
            _help.Close();
            return;
        }
        _help = new HelpWindow(_notes!.Settings, SetUiLanguage);
        _help.Closed += (_, _) =>
        {
            _help = null;
            TrimSoon(TimeSpan.FromSeconds(2)); // the paper picture is only kept while the card is open
        };
        _help.Show();
        _help.Activate();
    }

    internal void ExitApp()
    {
        Speech.Stop();
        _notes?.Shutdown();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _notes?.Shutdown();
        _hotkeys?.Dispose();
        _tray?.Dispose();
        foreach (var (signal, wait) in _signals)
        {
            wait.Unregister(null);
            signal.Dispose();
        }
        if (_instanceMutex is not null)
        {
            _instanceMutex.ReleaseMutex();
            _instanceMutex.Dispose();
        }
        base.OnExit(e);
    }

    void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Never lose notes to a bug: save what we have, log it, and keep running.
        try
        {
            _notes?.Flush();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
        LogError(e.Exception);
        e.Handled = true;
    }

    /// <summary>Appends to error.log, starting it over once it passes 1 MB.</summary>
    void LogError(Exception ex)
    {
        try
        {
            var log = new FileInfo(Path.Combine(_dataDirectory, "error.log"));
            if (log.Exists && log.Length > 1_000_000)
                log.Delete();
            File.AppendAllText(log.FullName, $"[{DateTime.Now:O}] {ex}\n\n");
        }
        catch (Exception logError) when (logError is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// %LOCALAPPDATA%\OpenNote. The app used to be called DeskNotes: its data folder is moved
    /// over once, so nobody loses notes when updating.
    /// </summary>
    static string DefaultDataDirectory()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var current = Path.Combine(local, "OpenNote");
        var legacy = Path.Combine(local, "DeskNotes");
        if (!Directory.Exists(current) && Directory.Exists(legacy))
        {
            // A DeskNotes still running would recreate its folder on its next save, stranding
            // those edits: ask it to save and quit first.
            var id = InstanceId(legacy);
            if (Mutex.TryOpenExisting($@"Local\DeskNotes-{id}", out var running))
            {
                using (running)
                {
                    if (EventWaitHandle.TryOpenExisting($@"Local\DeskNotes-exit-{id}", out var exit))
                    {
                        exit.Set();
                        exit.Dispose();
                    }
                    if (!WaitForExit(running))
                        return legacy;
                    running.ReleaseMutex();
                }
            }
            try
            {
                Directory.Move(legacy, current);
            }
            catch (IOException)
            {
                return legacy; // in use or locked: keep using it rather than start empty
            }
            catch (UnauthorizedAccessException)
            {
                return legacy;
            }
        }
        return current;
    }

    /// <summary>Waits (up to 10 s) until the running instance has saved and quit; true when it has.</summary>
    static bool WaitForExit(Mutex instance)
    {
        try
        {
            return instance.WaitOne(TimeSpan.FromSeconds(10));
        }
        catch (AbandonedMutexException)
        {
            return true; // it ended without releasing the mutex: gone all the same
        }
    }

    static string? ParseDataDirectory(string[] args)
    {
        int i = Array.IndexOf(args, "--data");
        return i >= 0 && i + 1 < args.Length ? Path.GetFullPath(args[i + 1]) : null;
    }

    static string InstanceId(string dataDirectory)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(dataDirectory.ToUpperInvariant()));
        return Convert.ToHexString(hash, 0, 8);
    }
}
