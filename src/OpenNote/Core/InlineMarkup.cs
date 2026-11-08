using System.Text;

namespace OpenNote.Core;

/// <summary>A run of text with its emphasis and its text color (a <see cref="TextColor.Tag"/>, null for the note's own ink).</summary>
public readonly record struct StyledSpan(string Text, bool Bold, bool Italic, string? Color = null);

/// <summary>
/// Bold and italic stored inside plain text, Markdown-style: **bold**, *italic*, ***both***.
/// Text color wraps them: {red}**bold red**{/}. A literal asterisk is written as \*, a literal
/// tag as \{red}, and backslashes right before an asterisk or a tag are doubled (so "path\" next
/// to bold text survives); other backslashes, as in C:\Users, are stored as is.
/// Notes stay readable plain text on disk and in search.
/// </summary>
public static class InlineMarkup
{
    const string CloseTag = "{/}";
    static readonly string[] OpenTags = Palette.TextColors.Select(c => "{" + c.Tag + "}").ToArray();

    public static List<StyledSpan> Parse(string text)
    {
        // Most lines have no markup at all: only \, * and { can start any.
        if (!text.AsSpan().ContainsAny('*', '\\', '{'))
            return text.Length == 0 ? [] : [new StyledSpan(text, false, false)];
        var spans = new List<StyledSpan>();
        int from = 0, i = 0;
        // Once no "{/}" is left after some point, none is after any later point either: stop looking,
        // or text full of unclosed tags (a pasted or imported note) would take quadratic time.
        bool noCloseLeft = false;
        while (i < text.Length)
        {
            if (text[i] == '\\')
            {
                i = SkipBackslashes(text, i);
                continue;
            }
            int k = text[i] == '{' && !noCloseLeft ? OpenTagAt(text, i) : -1; // every colour tag starts with {
            int start = k >= 0 ? i + OpenTags[k].Length : i;
            int close = k >= 0 ? FindCloseTag(text, start) : -1;
            noCloseLeft |= k >= 0 && close < 0;
            if (close > start)
            {
                ParseEmphasis(text, from, i, null, spans);
                ParseEmphasis(text, start, close, Palette.TextColors[k].Tag, spans);
                i = from = close + CloseTag.Length;
                continue;
            }
            i++;
        }
        ParseEmphasis(text, from, text.Length, null, spans);
        return spans;
    }

    /// <summary>Bold and italic in text[start..end], all in one color.</summary>
    static void ParseEmphasis(string text, int start, int end, string? color, List<StyledSpan> spans)
    {
        var plain = new StringBuilder();
        int i = start;
        while (i < end)
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
                while (i + run < end && text[i + run] == '*' && run < 3)
                    run++;
                int contentStart = i + run;
                int close = contentStart < end && !char.IsWhiteSpace(text[contentStart])
                    ? FindMarker(text, contentStart, end, run)
                    : -1;
                if (close > contentStart)
                {
                    if (plain.Length > 0)
                    {
                        spans.Add(new StyledSpan(plain.ToString(), false, false, color));
                        plain.Clear();
                    }
                    spans.Add(new StyledSpan(Unescape(text, contentStart, close), run >= 2, run != 2, color));
                    i = close + run;
                    continue;
                }
            }
            plain.Append(c);
            i++;
        }
        if (plain.Length > 0)
            spans.Add(new StyledSpan(plain.ToString(), false, false, color));
    }

    public static string Write(IEnumerable<StyledSpan> spans)
    {
        // Text pieces and marker pieces in order; escaping needs to see what follows each backslash.
        var pieces = new List<(string Text, bool IsMarker)>();
        string? color = null; // the color tag left open
        foreach (var span in Merge(spans))
        {
            if (span.Color != color)
            {
                if (color is not null)
                    pieces.Add((CloseTag, true));
                if (span.Color is not null)
                    pieces.Add(("{" + span.Color + "}", true));
                color = span.Color;
            }
            var marker = span.Bold && span.Italic ? "***" : span.Bold ? "**" : span.Italic ? "*" : "";
            var core = marker.Length == 0 ? "" : span.Text.Trim();
            if (core.Length == 0)
            {
                pieces.Add((span.Text, false));
                continue;
            }
            // Keep surrounding spaces outside the markers ("** a**" would not read back as bold).
            int lead = span.Text.Length - span.Text.AsSpan().TrimStart().Length;
            int trail = span.Text.Length - lead - core.Length;
            pieces.Add((span.Text[..lead], false));
            pieces.Add((marker, true));
            pieces.Add((core, false));
            pieces.Add((marker, true));
            pieces.Add((span.Text[^trail..], false));
        }
        if (color is not null)
            pieces.Add((CloseTag, true));

        var sb = new StringBuilder();
        int backslashes = 0; // literal backslashes not written yet
        foreach (var (text, isMarker) in pieces)
        {
            for (int k = 0; k < text.Length; k++)
            {
                char c = text[k];
                if (c == '\\' && !isMarker)
                {
                    backslashes++;
                    continue;
                }
                bool special = c == '*' || (c == '{' && IsTagAt(text, k));
                sb.Append('\\', special ? backslashes * 2 : backslashes);
                backslashes = 0;
                if (special && !isMarker)
                    sb.Append('\\');
                sb.Append(c);
            }
        }
        sb.Append('\\', backslashes);
        return sb.ToString();
    }

    /// <summary>The text without any markers, for titles, previews and search.</summary>
    public static string Strip(string text) =>
        text.AsSpan().ContainsAny('*', '\\', '{') ? string.Concat(Parse(text).Select(s => s.Text)) : text;

    static IEnumerable<StyledSpan> Merge(IEnumerable<StyledSpan> spans)
    {
        StyledSpan? current = null;
        foreach (var s in spans)
        {
            if (s.Text.Length == 0)
                continue;
            if (current is { } c && c.Bold == s.Bold && c.Italic == s.Italic && c.Color == s.Color)
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

    /// <summary>Which color tag ("{red}"…) starts at <paramref name="i"/>, or -1.</summary>
    static int OpenTagAt(string text, int i)
    {
        for (int k = 0; k < OpenTags.Length; k++)
            if (text.AsSpan(i).StartsWith(OpenTags[k], StringComparison.Ordinal))
                return k;
        return -1;
    }

    static bool IsTagAt(string text, int i) =>
        OpenTagAt(text, i) >= 0 || text.AsSpan(i).StartsWith(CloseTag, StringComparison.Ordinal);

    /// <summary>Next unescaped "{/}".</summary>
    static int FindCloseTag(string text, int from)
    {
        int i = from;
        while (i < text.Length)
        {
            if (text[i] == '\\')
            {
                i = SkipBackslashes(text, i);
                continue;
            }
            if (text.AsSpan(i).StartsWith(CloseTag, StringComparison.Ordinal))
                return i;
            i++;
        }
        return -1;
    }

    /// <summary>Steps over a run of backslashes, and over the tag's "{" when they escape it.</summary>
    static int SkipBackslashes(string text, int i)
    {
        int run = BackslashRun(text, i);
        i += run;
        return run % 2 == 1 && IsTagAt(text, i) ? i + 1 : i;
    }

    /// <summary>Next unescaped run of exactly <paramref name="count"/> asterisks' worth of marker, before <paramref name="end"/>.</summary>
    static int FindMarker(string text, int from, int end, int count)
    {
        for (int i = from; i <= end - count; i++)
        {
            if (text[i] == '\\')
            {
                int run = BackslashRun(text, i);
                i += run - 1;
                if (run % 2 == 1 && i + 1 < end && text[i + 1] == '*')
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
    /// on. Before an asterisk or a color tag, pairs stand for one backslash and an odd one out
    /// escapes it; elsewhere backslashes are literal.
    /// </summary>
    static int DecodeBackslashes(string text, int i, StringBuilder into)
    {
        int run = BackslashRun(text, i);
        int next = i + run;
        if (next >= text.Length || (text[next] != '*' && !IsTagAt(text, next)))
        {
            into.Append('\\', run);
            return next;
        }
        into.Append('\\', run / 2);
        if (run % 2 == 0)
            return next; // the asterisk or tag is a marker (or plain text if nothing closes it)
        into.Append(text[next]);
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
