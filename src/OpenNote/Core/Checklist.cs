namespace OpenNote.Core;

/// <summary>A text replacement to apply to the editor (kept as an edit so undo still works).</summary>
public readonly record struct TextEdit(int Start, int Length, string Replacement, int CaretAfter);

/// <summary>
/// Lightweight lists inside plain text: a line starting with "☐ " is an open item, "☑ " a
/// done item, "• " a bullet. The note stays plain text, so nothing is lost if the feature changes.
/// </summary>
public static class Checklist
{
    public const char Open = '☐';
    public const char Done = '☑';
    public const char Bullet = '•';
    const int PrefixLength = 2; // marker + space

    /// <summary>Plain line → open item; bullet → open item; open ↔ done.</summary>
    public static TextEdit Toggle(string text, int caret)
    {
        int start = LineStart(text, caret);
        if (HasMarker(text, start, out var marker))
            return new TextEdit(start, 1, (marker == Open ? Done : Open).ToString(), caret);
        return new TextEdit(start, 0, $"{Open} ", caret + PrefixLength);
    }

    /// <summary>True when <paramref name="index"/> is the box character at the start of a checklist line.</summary>
    public static bool IsBoxAt(string text, int index) =>
        index >= 0 && index < text.Length && LineStart(text, index) == index
        && HasMarker(text, index, out var marker) && marker != Bullet;

    /// <summary>
    /// Enter on a list line starts a new item of the same kind (a done item continues as an
    /// open one); Enter on an empty item removes the marker, ending the list.
    /// Returns null when Enter should behave normally.
    /// </summary>
    public static TextEdit? ContinueOnEnter(string text, int caret, string newline = "\r\n")
    {
        int start = LineStart(text, caret);
        if (!HasMarker(text, start, out var marker) || caret < start + PrefixLength)
            return null;

        int end = LineEnd(text, start);
        if (text.AsSpan(start + PrefixLength, end - start - PrefixLength).IsWhiteSpace())
            return new TextEdit(start, end - start, "", start);

        var insert = newline + (marker == Bullet ? Bullet : Open) + " ";
        return new TextEdit(caret, 0, insert, caret + insert.Length);
    }

    /// <summary>
    /// Every checklist item's text, after its "☐ "/"☑ " marker up to the end of its line, and
    /// whether it is done (a done item is shown struck through).
    /// </summary>
    public static IEnumerable<(int Start, int End, bool Done)> Items(string text)
    {
        for (int start = 0; start <= text.Length; start = LineEnd(text, start) + 1)
        {
            if (HasMarker(text, start, out var marker) && marker != Bullet)
                yield return (start + PrefixLength, LineEnd(text, start), marker == Done);
            if (LineEnd(text, start) >= text.Length)
                yield break;
        }
    }

    static bool HasMarker(string text, int lineStart, out char marker)
    {
        marker = default;
        if (lineStart + PrefixLength > text.Length || text[lineStart + 1] != ' ')
            return false;
        marker = text[lineStart];
        return marker is Open or Done or Bullet;
    }

    static int LineStart(string text, int index)
    {
        index = Math.Clamp(index, 0, text.Length);
        return index == 0 ? 0 : text.LastIndexOf('\n', index - 1) + 1;
    }

    static int LineEnd(string text, int lineStart)
    {
        int i = text.IndexOfAny(['\r', '\n'], lineStart);
        return i < 0 ? text.Length : i;
    }
}
