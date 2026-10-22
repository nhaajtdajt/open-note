using OpenNote.Core;

namespace OpenNote.Tests;

public class NoteVisibilityTests
{
    static readonly Guid A = Guid.NewGuid(), B = Guid.NewGuid(), C = Guid.NewGuid();
    static HashSet<Guid> Set(params Guid[] ids) => [.. ids];

    [Fact]
    public void MasterShortcut_HidesOnlyWhenEveryNoteIsShowing()
    {
        Assert.True(NoteVisibility.ShouldHideAll([true, true, true]));
        Assert.False(NoteVisibility.ShouldHideAll([false, false, false]));          // all hidden: show
        Assert.False(NoteVisibility.ShouldHideAll([true, false, false]));          // only a selection showing: show the rest
        Assert.False(NoteVisibility.ShouldHideAll([]));                            // no notes: nothing to hide
    }

    [Fact]
    public void SavedShortcut_PicksFirst_ThenShowsSelection_ThenHides()
    {
        Assert.Equal(FocusAction.Pick, NoteVisibility.ForFocusShortcut(Set(A, B, C), Set()));
        Assert.Equal(FocusAction.ShowSet, NoteVisibility.ForFocusShortcut(Set(A, B, C), Set(A)));   // everything showing: narrow down
        Assert.Equal(FocusAction.ShowSet, NoteVisibility.ForFocusShortcut(Set(), Set(A, B)));       // all hidden: bring the selection back
        Assert.Equal(FocusAction.HideAll, NoteVisibility.ForFocusShortcut(Set(A, B), Set(A, B)));   // exactly the selection showing
    }

    [Fact]
    public void SavedSelection_IsStoredOnce_AndSurvivesReload()
    {
        var dir = Path.Combine(Path.GetTempPath(), "OpenNoteTests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new NoteStore(dir);
            store.Save(new StoreData { Notes = [new Note { Id = A, Text = "a" }], FocusSet = [A, B] });
            var loaded = new NoteStore(dir).Load();
            Assert.Equal([A, B], loaded.FocusSet);

            File.WriteAllText(store.FilePath, """{ "version": 1, "notes": [] }""");   // files from before this feature
            Assert.Empty(new NoteStore(dir).Load().FocusSet);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
