namespace OpenNote.Core;

/// <summary>
/// What Check with AI sends along with the text so the AI knows what the note is about: the
/// note's title and the last words written before the text. The AI only reads it for meaning;
/// it never corrects or translates it.
/// </summary>
public static class AiContext
{
    public const int MaxWords = 30;

    /// <summary>
    /// "Title: …" and "Just before: …" (the last <see cref="MaxWords"/> words, list markers and
    /// pictures left out); empty when the note has neither.
    /// </summary>
    public static string Build(string title, string before)
    {
        var words = before
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w is not ([Checklist.Open] or [Checklist.Done] or [Checklist.Bullet]) && !w.Contains('￼'))
            .ToList();
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(title))
            lines.Add("Title: " + title.Trim());
        if (words.Count > 0)
            lines.Add("Just before: " + (words.Count > MaxWords ? "… " : "") + string.Join(' ', words.TakeLast(MaxWords)));
        return string.Join("\n", lines);
    }
}
