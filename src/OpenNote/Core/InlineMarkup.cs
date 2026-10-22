using System.Text;

namespace OpenNote.Core;

/// <summary>A run of text with its emphasis.</summary>
public readonly record struct StyledSpan(string Text, bool Bold, bool Italic);

/// <summary>
/// Bold and italic stored inside plain text, Markdown-style: **bold**, *italic*, ***both***.
/// A literal asterisk is written as \*, and backslashes right before an asterisk are doubled
/// (so "path\" next to bold text survives); other backslashes, as in C:\Users, are stored as is.
/// Notes stay readable plain text on disk and in search.
/// </summary>
public static class InlineMarkup
{
    public static List<StyledSpan> Parse(string text)
    {
        var spans = new List<StyledSpan>();
        var plain = new StringBuilder();
        int i = 0;
        while (i < text.Length)
        {
            char c = text[i];
            if (c == '\\')
            {
                i = DecodeBackslashes(text, i, plain);
                continue;
            }
            if (c == '*')
            {
                int run = 1;
                while (i + run < text.Length && text[i + run] == '*' && run < 3)
                    run++;
                int contentStart = i + run;
                int close = contentStart < text.Length && !char.IsWhiteSpace(text[contentStart])
                    ? FindMarker(text, contentStart, run)
                    : -1;
                if (close > contentStart)
                {
                    if (plain.Length > 0)
                    {
                        spans.Add(new StyledSpan(plain.ToString(), false, false));
                        plain.Clear();
                    }
                    spans.Add(new StyledSpan(Unescape(text, contentStart, close), run >= 2, run != 2));
                    i = close + run;
                    continue;
                }
            }
            plain.Append(c);
            i++;
        }
        if (plain.Length > 0)
            spans.Add(new StyledSpan(plain.ToString(), false, false));
        return spans;
    }

    public static string Write(IEnumerable<StyledSpan> spans)
    {
        // Text pieces and marker pieces in order; escaping needs to see what follows each backslash.
        var pieces = new List<(string Text, bool IsMarker)>();
        foreach (var span in Merge(spans))
        {
            var marker = span.Bold && span.Italic ? "***" : span.Bold ? "**" : span.Italic ? "*" : "";
            var core = span.Text.Trim();
            if (marker.Length == 0 || core.Length == 0)
            {
                pieces.Add((span.Text, false));
                continue;
            }
            // Keep surrounding spaces outside the markers ("** a**" would not read back as bold).
            int lead = span.Text.Length - span.Text.TrimStart().Length;
            int trail = span.Text.Length - span.Text.TrimEnd().Length;
            pieces.Add((span.Text[..lead], false));
            pieces.Add((marker, true));
            pieces.Add((core, false));
            pieces.Add((marker, true));
            pieces.Add((span.Text[^trail..], false));
        }

        var sb = new StringBuilder();
        int backslashes = 0; // literal backslashes not written yet
        foreach (var (text, isMarker) in pieces)
        {
            foreach (char c in text)
            {
                if (c == '\\' && !isMarker)
                {
                    backslashes++;
                    continue;
                }
                bool asterisk = c == '*';
                sb.Append('\\', asterisk ? backslashes * 2 : backslashes);
                backslashes = 0;
                if (asterisk && !isMarker)
                    sb.Append('\\');
                sb.Append(c);
            }
        }
        sb.Append('\\', backslashes);
        return sb.ToString();
    }

    /// <summary>The text without any markers, for titles, previews and search.</summary>
    public static string Strip(string text) => string.Concat(Parse(text).Select(s => s.Text));

    static IEnumerable<StyledSpan> Merge(IEnumerable<StyledSpan> spans)
    {
        StyledSpan? current = null;
        foreach (var s in spans)
        {
            if (s.Text.Length == 0)
                continue;
            if (current is { } c && c.Bold == s.Bold && c.Italic == s.Italic)
                current = c with { Text = c.Text + s.Text };
            else
            {
                if (current is { } done)
                    yield return done;
                current = s;
            }
        }
        if (current is { } last)
            yield return last;
    }

    /// <summary>Next unescaped run of exactly <paramref name="count"/> asterisks' worth of marker.</summary>
    static int FindMarker(string text, int from, int count)
    {
        for (int i = from; i <= text.Length - count; i++)
        {
            if (text[i] == '\\')
            {
                int run = BackslashRun(text, i);
                i += run - 1;
                if (run % 2 == 1 && i + 1 < text.Length && text[i + 1] == '*')
                    i++; // an escaped asterisk is text, not a marker
                continue;
            }
            if (text[i] != '*')
                continue;
            bool match = true;
            for (int k = 1; k < count; k++)
                match &= text[i + k] == '*';
            if (match && i > from)
                return i;
        }
        return -1;
    }

    /// <summary>The text of text[start..end] with its escapes decoded.</summary>
    static string Unescape(string text, int start, int end)
    {
        var sb = new StringBuilder(end - start);
        for (int i = start; i < end;)
        {
            if (text[i] == '\\')
                i = DecodeBackslashes(text, i, sb);
            else
                sb.Append(text[i++]);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Decodes the backslashes starting at <paramref name="i"/> and returns where decoding goes
    /// on. Before an asterisk, pairs stand for one backslash and an odd one out escapes the
    /// asterisk; elsewhere backslashes are literal.
    /// </summary>
    static int DecodeBackslashes(string text, int i, StringBuilder into)
    {
        int run = BackslashRun(text, i);
        int next = i + run;
        if (next >= text.Length || text[next] != '*')
        {
            into.Append('\\', run);
            return next;
        }
        into.Append('\\', run / 2);
        if (run % 2 == 0)
            return next; // the asterisk is a marker (or plain text if nothing closes it)
        into.Append('*');
        return next + 1;
    }

    static int BackslashRun(string text, int i)
    {
        int run = 0;
        while (i + run < text.Length && text[i + run] == '\\')
            run++;
        return run;
    }
}
