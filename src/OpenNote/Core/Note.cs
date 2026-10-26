using System.Text.Json.Serialization;

namespace OpenNote.Core;

public enum NoteFont
{
    /// <summary>Consolas: code-editor look, every character the same width.</summary>
    Mono,
    Sans,
    /// <summary>Ink Free, bundled with Windows.</summary>
    Hand,
}

public enum NoteState
{
    /// <summary>Has a window on the desktop (it may be temporarily hidden by "hide all").</summary>
    Visible,
    /// <summary>Closed with the x button: no window, but still in the notes list (not in the Archive). Reopen it from the list.</summary>
    Hidden,
    /// <summary>Soft-deleted; restorable from the Archive tab until deleted permanently.</summary>
    Archived,
}

public sealed class Note
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Title shown in the note's header; empty means "untitled".</summary>
    public string Heading { get; set; } = "";

    /// <summary>The body, with **bold** and *italic* kept as inline markers (see InlineMarkup).</summary>
    public string Text { get; set; } = "";
    public string Color { get; set; } = Palette.Default.Hex;

    /// <summary>How solid the glass is (0.2–1). Text always stays fully opaque.</summary>
    public double Opacity { get; set; } = 0.9;

    public double FontSize { get; set; } = 14;

    public NoteFont Font { get; set; } = NoteFont.Mono;

    /// <summary>Spell checking: null = automatic (on unless the note is Vietnamese), else forced on/off.</summary>
    public bool? SpellCheck { get; set; }

    /// <summary>Read-only migration of the old on/off handwriting flag; never written.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool? Handwriting { get; set; }

    // Window bounds in physical pixels (virtual-screen coordinates). Width == 0 means "never placed".
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    /// <summary>
    /// Size (physical pixels) to return to on the next double-click of the header, set while the
    /// note is shrunk to the normal size. 0 = nothing to restore.
    /// </summary>
    public int RestoreWidth { get; set; }
    public int RestoreHeight { get; set; }

    public bool IsLocked { get; set; }

    /// <summary>Marked with the star in the top-left corner: a note that matters. Listed first in the notes list.</summary>
    public bool IsImportant { get; set; }

    public NoteState State { get; set; } = NoteState.Visible;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public bool IsEmpty => string.IsNullOrWhiteSpace(Text) && string.IsNullOrWhiteSpace(Heading);

    /// <summary>The heading if set, else the first non-blank line; used as the note's name in lists.</summary>
    [JsonIgnore]
    public string Title => !string.IsNullOrWhiteSpace(Heading) ? Heading.Trim() : Lines().FirstOrDefault() ?? "";

    /// <summary>The body lines not already used as the title, joined for a one-line preview.</summary>
    [JsonIgnore]
    public string Preview => string.Join("  /  ", Lines().Skip(string.IsNullOrWhiteSpace(Heading) ? 1 : 0).Take(3));

    IEnumerable<string> Lines() =>
        Text.Split('\n')
            .Select(l => ImageStore.IsImageLine(l, out _) ? Ui.T("[image]") : InlineMarkup.Strip(l).Trim())
            .Where(l => l.Length > 0);
}

public sealed class StoreData
{
    public int Version { get; set; } = 1;
    public List<Note> Notes { get; set; } = [];

    /// <summary>
    /// The one saved selection: notes to show (and keep alone on screen) with the "saved selection"
    /// shortcut. Replaced whenever the selection is picked again.
    /// </summary>
    public List<Guid> FocusSet { get; set; } = [];
}
