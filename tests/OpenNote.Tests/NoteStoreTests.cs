using OpenNote.Core;

namespace OpenNote.Tests;

public sealed class NoteStoreTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "OpenNoteTests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void FirstLoad_IsEmptyAndMarkedFirstRun()
    {
        var store = new NoteStore(_dir);
        var data = store.Load();
        Assert.Empty(data.Notes);
        Assert.True(store.IsFirstRun);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsEverything()
    {
        var note = new Note
        {
            Text = "Ghi chú tiếng Việt\r\n☐ mua sữa",
            Color = "#C3E4FF", Opacity = 0.6, FontSize = 18,
            X = -1500, Y = 40, Width = 300, Height = 280,
            IsLocked = true, State = NoteState.Archived,
        };
        new NoteStore(_dir).Save(new StoreData { Notes = [note] });

        var store = new NoteStore(_dir);
        var loaded = Assert.Single(store.Load().Notes);
        Assert.False(store.IsFirstRun);
        Assert.Equal(note.Id, loaded.Id);
        Assert.Equal(note.Text, loaded.Text);
        Assert.Equal((note.Color, note.Opacity, note.FontSize), (loaded.Color, loaded.Opacity, loaded.FontSize));
        Assert.Equal((note.X, note.Y, note.Width, note.Height), (loaded.X, loaded.Y, loaded.Width, loaded.Height));
        Assert.Equal((true, NoteState.Archived), (loaded.IsLocked, loaded.State));
    }

    [Fact]
    public void OldNotes_MigrateHandwritingFlag_AndDefaultToMono()
    {
        Directory.CreateDirectory(_dir);
        var store = new NoteStore(_dir);
        File.WriteAllText(store.FilePath, """
            { "version": 1, "notes": [
              { "text": "a", "handwriting": true },
              { "text": "b", "handwriting": false } ] }
            """);

        var notes = store.Load().Notes;

        Assert.Equal(NoteFont.Hand, notes[0].Font);
        Assert.Equal(NoteFont.Mono, notes[1].Font);
        Assert.All(notes, n => Assert.Equal("", n.Heading));

        store.Save(new StoreData { Notes = notes });
        Assert.DoesNotContain("handwriting", File.ReadAllText(store.FilePath));
    }

    [Fact]
    public void ClosedNotes_StayClosed_AfterReload()
    {
        Directory.CreateDirectory(_dir);
        var store = new NoteStore(_dir);
        File.WriteAllText(store.FilePath, """
            { "version": 1, "notes": [
              { "text": "a", "state": "Hidden" },
              { "text": "b", "state": "Archived" } ] }
            """);
        var notes = store.Load().Notes;
        Assert.Equal(NoteState.Hidden, notes[0].State);
        Assert.Equal(NoteState.Archived, notes[1].State);
    }

    [Fact]
    public void Heading_RoundTrips_AndIsUsedAsTitle()
    {
        var store = new NoteStore(_dir);
        store.Save(new StoreData { Notes = [new Note { Heading = "Việc cần làm", Text = "☐ a\r\n☐ b" }] });
        var note = Assert.Single(new NoteStore(_dir).Load().Notes);
        Assert.Equal("Việc cần làm", note.Heading);
        Assert.Equal("Việc cần làm", note.Title);
        Assert.StartsWith("☐ a", note.Preview);
        Assert.True(NoteSearch.Matches(note, "viec can"));
    }

    [Fact]
    public void TheStar_IsOffByDefault_AndIsRemembered()
    {
        var store = new NoteStore(_dir);
        store.Save(new StoreData { Notes = [new Note { Text = "plain" }, new Note { Text = "matters", IsImportant = true }] });
        var notes = new NoteStore(_dir).Load().Notes;
        Assert.False(new Note().IsImportant);
        Assert.False(notes[0].IsImportant);
        Assert.True(notes[1].IsImportant);
    }

    [Fact]
    public void Save_KeepsNonAsciiReadableInFile()
    {
        var store = new NoteStore(_dir);
        store.Save(new StoreData { Notes = [new Note { Text = "Ghi chú" }] });
        Assert.Contains("Ghi chú", File.ReadAllText(store.FilePath));
    }

    [Fact]
    public void SecondSave_KeepsPreviousVersionAsBackup()
    {
        var store = new NoteStore(_dir);
        store.Save(new StoreData { Notes = [new Note { Text = "v1" }] });
        store.Save(new StoreData { Notes = [new Note { Text = "v2" }] });

        Assert.Contains("v2", File.ReadAllText(store.FilePath));
        Assert.Contains("v1", File.ReadAllText(store.BackupPath));
        Assert.False(File.Exists(store.FilePath + ".tmp"));
    }

    [Fact]
    public void CorruptFile_FallsBackToBackup_AndSetsCorruptFileAside()
    {
        var store = new NoteStore(_dir);
        store.Save(new StoreData { Notes = [new Note { Text = "good" }] });
        store.Save(new StoreData { Notes = [new Note { Text = "newer" }] });
        File.WriteAllText(store.FilePath, "{ this is not json");

        var data = new NoteStore(_dir).Load();

        Assert.Equal("good", Assert.Single(data.Notes).Text);
        Assert.Single(Directory.GetFiles(_dir, "notes.corrupt-*.json"));
    }

    [Fact]
    public void CorruptFileWithoutBackup_LoadsEmptyButKeepsTheFile()
    {
        Directory.CreateDirectory(_dir);
        var store = new NoteStore(_dir);
        File.WriteAllText(store.FilePath, "garbage");

        Assert.Empty(store.Load().Notes);
        Assert.False(store.IsFirstRun);
        Assert.Single(Directory.GetFiles(_dir, "notes.corrupt-*.json"));
    }

    [Fact]
    public void Settings_AreCreatedWithDefaults_AndHandEditsAreRead()
    {
        var created = AppSettings.Load(_dir, out var error);
        Assert.Null(error);
        Assert.Equal("Ctrl+Alt+N", created.NewNoteHotkey);
        Assert.True(File.Exists(AppSettings.PathIn(_dir)));

        File.WriteAllText(AppSettings.PathIn(_dir), """{ "newNoteHotkey": "Win+Shift+N" }""");
        var edited = AppSettings.Load(_dir, out error);
        Assert.Null(error);
        Assert.Equal("Win+Shift+N", edited.NewNoteHotkey);
        Assert.Equal("Ctrl+Alt+H", edited.ToggleAllHotkey);
    }

    [Fact]
    public void BrokenSettings_FallBackToDefaultsWithError()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(AppSettings.PathIn(_dir), "{ nope");
        var settings = AppSettings.Load(_dir, out var error);
        Assert.NotNull(error);
        Assert.True(settings.ReadFailed);
        Assert.Equal("Ctrl+Alt+N", settings.NewNoteHotkey);
    }

    [Fact]
    public void OnlyACleanLoad_AllowsCleaningUpPictures()
    {
        var store = new NoteStore(_dir);
        store.Load();
        Assert.True(store.LoadedCleanly); // first run

        store.Save(new StoreData { Notes = [new Note { Text = "old" }] });
        store.Save(new StoreData { Notes = [new Note { Text = "new" }] });
        var clean = new NoteStore(_dir);
        clean.Load();
        Assert.True(clean.LoadedCleanly);

        File.WriteAllText(store.FilePath, """{ "notes": [ { "text": "x", "font": "FromANewerVersion" } ] }""");
        var fallback = new NoteStore(_dir);
        Assert.Equal("old", Assert.Single(fallback.Load().Notes).Text);
        Assert.False(fallback.LoadedCleanly); // pictures only "new" used must survive
    }

    [Fact]
    public void UnreadableBackupToo_IsSetAside_SoLaterSavesDontOverwriteIt()
    {
        Directory.CreateDirectory(_dir);
        var store = new NoteStore(_dir);
        File.WriteAllText(store.FilePath, "garbage");
        File.WriteAllText(store.BackupPath, "more garbage");

        Assert.Empty(store.Load().Notes);
        store.Save(new StoreData());
        store.Save(new StoreData());

        Assert.Equal(2, Directory.GetFiles(_dir, "notes.corrupt-*").Length);
        Assert.Contains("more garbage", Directory.GetFiles(_dir, "notes.corrupt-*.bak.json").Select(File.ReadAllText));
    }

    [Fact]
    public void LockedNotesFile_IsNotMistakenForDamage()
    {
        var store = new NoteStore(_dir);
        store.Save(new StoreData { Notes = [new Note { Text = "a" }] });
        using (new FileStream(store.FilePath, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.Throws<IOException>(() => new NoteStore(_dir).Load());
        Assert.Empty(Directory.GetFiles(_dir, "notes.corrupt-*"));
        Assert.Equal("a", Assert.Single(new NoteStore(_dir).Load().Notes).Text);
    }

    [Fact]
    public void SettingsUpdate_KeepsHandEdits_AndAllowsCommentsAndTrailingCommas()
    {
        var settings = AppSettings.Load(_dir, out _);
        File.WriteAllText(AppSettings.PathIn(_dir), """
            {
              // my own shortcut
              "newNoteHotkey": "Win+Shift+N",
            }
            """);

        Assert.Null(settings.Update(_dir, s => s.KeepNotesOnTop = false));

        var reloaded = AppSettings.Load(_dir, out var error);
        Assert.Null(error);
        Assert.Equal("Win+Shift+N", reloaded.NewNoteHotkey); // not overwritten by the stale copy in memory
        Assert.False(reloaded.KeepNotesOnTop);
        Assert.False(settings.KeepNotesOnTop);
    }

    [Fact]
    public void FilesSavedWithAByteOrderMark_StillLoad()
    {
        Directory.CreateDirectory(_dir);
        var store = new NoteStore(_dir);
        var bom = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        File.WriteAllText(store.FilePath, """{ "notes": [ { "text": "Ghi chú" } ] }""", bom);
        File.WriteAllText(AppSettings.PathIn(_dir), """{ "newNoteHotkey": "Win+Shift+N" }""", bom);

        Assert.Equal("Ghi chú", Assert.Single(store.Load().Notes).Text);
        Assert.True(store.LoadedCleanly);
        Assert.Equal("Win+Shift+N", AppSettings.Load(_dir, out var error).NewNoteHotkey);
        Assert.Null(error);
    }

    [Fact]
    public void SettingsUpdate_NeverOverwritesABrokenFile()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(AppSettings.PathIn(_dir), """{ "geminiApiKeys": ["k1"] oops""");
        var settings = AppSettings.Load(_dir, out _);

        Assert.NotNull(settings.Update(_dir, s => s.AutoStartConfigured = true));
        Assert.Contains("\"k1\"", File.ReadAllText(AppSettings.PathIn(_dir)));
    }
}
