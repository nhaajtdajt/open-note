namespace OpenNote.Core;

/// <summary>
/// What an important note wears in its top-left corner: the star, or one of the flowers picked
/// from the star's menu. Stored in <see cref="Note.Mark"/> by id; anything else is the star.
/// </summary>
public static class NoteMarks
{
    /// <summary>The navy star (and what notes marked before the flowers existed keep).</summary>
    public const string Star = "";

    /// <summary>The flowers, in the order the picker shows them; each has a picture in Assets/marks/{Id}.png.</summary>
    public static readonly IReadOnlyList<(string Id, string Name)> Flowers =
    [
        ("pink", "Pink flower"),
        ("yellow", "Yellow flower"),
        ("blue", "Blue flower"),
        ("red", "Red flower"),
        ("daisy", "Daisy"),
        ("clover", "Four-leaf clover"),
    ];

    public static bool IsFlower(string? mark) => Flowers.Any(f => f.Id == mark);
}
