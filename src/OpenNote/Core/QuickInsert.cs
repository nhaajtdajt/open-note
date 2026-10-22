namespace OpenNote.Core;

/// <summary>
/// Notion-style typing shortcuts: type "/" then a key and the pair turns into a symbol.
/// "/-" → bullet, "/>" → checkbox.
/// </summary>
public static class QuickInsert
{
    public static readonly IReadOnlyDictionary<char, string> Shortcuts = new Dictionary<char, string>
    {
        ['-'] = Checklist.Bullet + " ",
        ['>'] = Checklist.Open + " ",
    };

    /// <summary>The edit to apply when <paramref name="typed"/> is about to be typed at <paramref name="caret"/>, or null.</summary>
    public static TextEdit? TryExpand(string text, int caret, string typed)
    {
        if (typed.Length != 1 || caret < 1 || caret > text.Length || text[caret - 1] != '/')
            return null;
        if (!Shortcuts.TryGetValue(typed[0], out var symbol))
            return null;
        // "//-" stays literal, so paths and URLs with "/-" can still be typed after a second slash.
        if (caret >= 2 && text[caret - 2] == '/')
            return null;
        return new TextEdit(caret - 1, 1, symbol, caret - 1 + symbol.Length);
    }
}
