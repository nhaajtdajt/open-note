using System.Globalization;

namespace OpenNote.Core;

public static class NoteSearch
{
    static readonly CompareInfo Compare = CultureInfo.InvariantCulture.CompareInfo;

    /// <summary>
    /// Case- and accent-insensitive "contains" for every word in the query,
    /// so "ghi chu" finds "Ghi chú" and "milk eggs" finds a note mentioning both.
    /// </summary>
    public static bool Matches(Note note, string? query) => Matches(note.Heading + "\n" + InlineMarkup.Strip(note.Text), query);

    public static bool Matches(string text, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;
        foreach (var word in query.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (Compare.IndexOf(text, word, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) < 0)
                return false;
        }
        return true;
    }
}
