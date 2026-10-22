namespace OpenNote.Core;

public enum FocusAction
{
    /// <summary>No saved selection yet: let the user pick one.</summary>
    Pick,
    /// <summary>Show only the saved notes and hide the rest.</summary>
    ShowSet,
    /// <summary>The saved notes are already the only ones showing: hide everything.</summary>
    HideAll,
}

/// <summary>Decisions behind the "hide/show all" and "saved selection" shortcuts.</summary>
public static class NoteVisibility
{
    /// <summary>
    /// The master shortcut hides when every note is showing, and shows all otherwise, so it also
    /// brings back the rest while only a selection is showing.
    /// </summary>
    public static bool ShouldHideAll(IReadOnlyCollection<bool> noteIsShowing) =>
        noteIsShowing.Count > 0 && noteIsShowing.All(shown => shown);

    public static FocusAction ForFocusShortcut(IReadOnlySet<Guid> showing, IReadOnlySet<Guid> saved)
    {
        if (saved.Count == 0)
            return FocusAction.Pick;
        return showing.SetEquals(saved) ? FocusAction.HideAll : FocusAction.ShowSet;
    }
}
