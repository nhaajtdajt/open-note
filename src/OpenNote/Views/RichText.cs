using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using OpenNote.Core;

namespace OpenNote.Views;

/// <summary>
/// Bridges the note's stored text (plain text with **bold**/*italic* markers, one line per
/// paragraph, a picture as its own "![image](…)" line) and the editor's FlowDocument, and maps
/// plain-text offsets inside a paragraph to TextPointers so the plain-text list logic
/// (Checklist, QuickInsert) works on rich text.
/// <para>
/// Pictures live <em>inside</em> a paragraph (an InlineUIContainer, like one big character), not
/// as separate blocks: that way the caret moves past them, Backspace/Delete remove them and
/// Ctrl+Z brings them back, exactly like text. A WPF block-level picture (BlockUIContainer)
/// can't be typed around or deleted with the keyboard.
/// </para>
/// </summary>
internal static class RichText
{
    /// <summary>Stands for a picture in <see cref="PlainText"/>, so offsets stay in step.</summary>
    public const char PictureChar = '￼';

    /// <param name="createImage">Builds the picture for a picture line; null shows picture lines as text.</param>
    /// <param name="darkPaper">Draws text colors in their shades for a dark note.</param>
    public static void Load(FlowDocument doc, string text, Func<string, InlineUIContainer>? createImage = null, bool darkPaper = false)
    {
        doc.Blocks.Clear();
        foreach (var line in text.Split('\n'))
        {
            if (createImage is not null && ImageStore.IsImageLine(line, out var path))
            {
                doc.Blocks.Add(PictureParagraph(createImage(path)));
                continue;
            }
            var paragraph = new Paragraph();
            foreach (var span in InlineMarkup.Parse(line.TrimEnd('\r')))
            {
                var run = StyledRun(span.Text, span.Bold, span.Italic);
                if (span.Color is not null)
                    run.Foreground = TextBrush(span.Color, darkPaper);
                paragraph.Inlines.Add(run);
            }
            doc.Blocks.Add(paragraph);
            MarkDoneItems(paragraph);
        }
    }

    // Frozen: shared by every note, and usable from any UI thread.
    static readonly TextDecorationCollection NoDecorations = (TextDecorationCollection)new TextDecorationCollection().GetAsFrozen();

    /// <summary>
    /// Strikes through the text of done checklist items ("☑ ") and clears it from open ones.
    /// Drawn from the box every time, never saved: the note text stays plain.
    /// </summary>
    public static void MarkDoneItems(Paragraph p)
    {
        foreach (var (start, end, done) in Checklist.Items(PlainText(p)))
        {
            if (end > start)
                new TextRange(PositionAt(p, start), PositionAt(p, end))
                    .ApplyPropertyValue(Inline.TextDecorationsProperty, done ? TextDecorations.Strikethrough : NoDecorations);
        }
    }

    /// <summary>
    /// A line holding a picture. Its line height follows the picture: the note's fixed line
    /// spacing would otherwise squash the picture into one text line's height.
    /// </summary>
    public static Paragraph PictureParagraph(InlineUIContainer picture) =>
        new(picture) { LineStackingStrategy = LineStackingStrategy.MaxHeight };

    public static Run StyledRun(string text, bool bold, bool italic) => new(text)
    {
        FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
        FontStyle = italic ? FontStyles.Italic : FontStyles.Normal,
    };

    public static string Save(FlowDocument doc)
    {
        var lines = new List<string>();
        foreach (var block in doc.Blocks)
            AddBlock(block, lines);
        return string.Join("\r\n", lines);
    }

    /// <summary>All of a document's text as styled spans, lines joined by CRLF (used to keep bold/italic when pasting).</summary>
    public static List<StyledSpan> Spans(FlowDocument doc)
    {
        var spans = new List<StyledSpan>();
        bool first = true;
        foreach (var block in doc.Blocks)
        {
            var lines = new List<string>();
            AddBlock(block, lines);
            foreach (var line in lines)
            {
                if (!first)
                    spans.Add(new StyledSpan("\r\n", false, false));
                spans.AddRange(InlineMarkup.Parse(line));
                first = false;
            }
        }
        return spans;
    }

    /// <summary>Every picture in the document, in order.</summary>
    public static IEnumerable<InlineUIContainer> Pictures(FlowDocument doc) =>
        doc.Blocks.OfType<Paragraph>()
            .SelectMany(p => Items(p.Inlines))
            .OfType<InlineUIContainer>()
            .Where(c => c.Child is PictureView);

    /// <summary>True when the range contains at least one picture.</summary>
    public static bool HasPicture(FlowDocument doc, TextPointer start, TextPointer end) =>
        Pictures(doc).Any(c => start.CompareTo(c.ElementEnd) < 0 && end.CompareTo(c.ElementStart) > 0);

    /// <summary>
    /// Removes the pictures in a range (or one given picture) by hand. WPF can't delete a picture
    /// itself: to support Ctrl+Z it saves a XAML copy of everything it deletes, and a picture
    /// doesn't survive that round trip (it comes back as an empty Grid). So pictures are taken out
    /// with the editor's undo history switched off, and the history restarts from here.
    /// </summary>
    public static void RemovePictures(RichTextBox editor, TextPointer start, TextPointer end, InlineUIContainer? only = null)
    {
        var doomed = Pictures(editor.Document)
            .Where(c => only is null
                ? start.CompareTo(c.ElementEnd) < 0 && end.CompareTo(c.ElementStart) > 0
                : c == only)
            .ToList();
        if (doomed.Count == 0)
            return;

        bool undoWasOn = editor.IsUndoEnabled;
        editor.IsUndoEnabled = false;
        try
        {
            foreach (var picture in doomed)
            {
                if (picture.Parent is not Paragraph paragraph)
                    continue;
                paragraph.Inlines.Remove(picture);
                // A line that held only the picture goes away with it (but the note keeps one paragraph).
                if (!paragraph.Inlines.Any() && editor.Document.Blocks.Count > 1)
                    editor.Document.Blocks.Remove(paragraph);
            }
        }
        finally
        {
            editor.IsUndoEnabled = undoWasOn;
        }
    }

    static void AddBlock(Block block, List<string> lines)
    {
        switch (block)
        {
            case Paragraph p:
                AddParagraph(p, lines);
                break;
            case List list:
                foreach (var item in list.ListItems)
                    foreach (var child in item.Blocks)
                        AddBlock(child, lines);
                break;
            case Section section:
                foreach (var child in section.Blocks)
                    AddBlock(child, lines);
                break;
            default:
                lines.Add(new TextRange(block.ContentStart, block.ContentEnd).Text.TrimEnd('\r', '\n'));
                break;
        }
    }

    /// <summary>
    /// One paragraph becomes one line, except that every picture gets a line of its own: text
    /// typed right next to a picture is saved on the line before or after it.
    /// </summary>
    static void AddParagraph(Paragraph p, List<string> lines)
    {
        var spans = new List<StyledSpan>();
        bool hadPicture = false;

        void Flush(bool evenIfEmpty)
        {
            if (spans.Count == 0 && !evenIfEmpty)
                return;
            lines.AddRange(InlineMarkup.Write(spans).Split('\n'));
            spans.Clear();
        }

        void Walk(InlineCollection inlines)
        {
            foreach (var inline in inlines)
            {
                switch (inline)
                {
                    case Run run:
                        spans.Add(new StyledSpan(run.Text, IsBold(run), IsItalic(run), ColorOf(run.Foreground)));
                        break;
                    case LineBreak:
                        spans.Add(new StyledSpan("\n", false, false));
                        break;
                    case InlineUIContainer { Child: PictureView picture }:
                        Flush(evenIfEmpty: false);
                        lines.Add(ImageStore.Line(picture.RelativePath));
                        hadPicture = true;
                        break;
                    case Span span: // Bold, Italic, Hyperlink... the runs inside report the effective style
                        Walk(span.Inlines);
                        break;
                }
            }
        }

        Walk(p.Inlines);
        Flush(evenIfEmpty: !hadPicture); // an empty paragraph is an empty line; a picture line isn't followed by one
    }

    // ---------- Text colors ----------

    static readonly Dictionary<(string, bool), SolidColorBrush> TextBrushes = Palette.TextColors
        .SelectMany(c => new[] { ((c.Tag, false), c.Hex), ((c.Tag, true), c.DarkPaperHex) })
        .ToDictionary(x => x.Item1, x => (SolidColorBrush)new SolidColorBrush(ToColor(x.Item2)).GetAsFrozen());

    static Color ToColor(string hex)
    {
        var (r, g, b) = Palette.ParseRgb(hex);
        return Color.FromRgb(r, g, b);
    }

    /// <summary>The brush of a text color (a <see cref="TextColor.Tag"/>) on a light or a dark note.</summary>
    public static SolidColorBrush TextBrush(string tag, bool darkPaper) => TextBrushes[(tag, darkPaper)];

    /// <summary>Which text color a brush draws (in either shade), or null for anything else, such as the note's ink.</summary>
    public static string? ColorOf(Brush? brush)
    {
        if (brush is SolidColorBrush b)
            foreach (var (key, shade) in TextBrushes) // runs for every run on every save: no LINQ
                if (shade.Color == b.Color)
                    return key.Item1;
        return null;
    }

    /// <summary>
    /// Redraws every text color in the shade for the note's paper, and clears any other color set
    /// on the text (the ink a "No color" leaves behind), so the text follows the note's ink again.
    /// Only touches what differs, so it adds nothing to the undo history when all is right.
    /// </summary>
    public static void FixTextColors(FlowDocument doc, bool darkPaper)
    {
        foreach (var p in doc.Blocks.OfType<Paragraph>())
        {
            Fix(p);
            foreach (var inline in Elements(p.Inlines))
                Fix(inline);
        }

        void Fix(TextElement e)
        {
            if (e.ReadLocalValue(TextElement.ForegroundProperty) is not Brush local)
                return;
            if (ColorOf(local) is not { } tag)
                e.ClearValue(TextElement.ForegroundProperty);
            else if (TextBrush(tag, darkPaper) is var want && ((SolidColorBrush)local).Color != want.Color)
                e.Foreground = want;
        }

        static IEnumerable<Inline> Elements(InlineCollection inlines)
        {
            foreach (var inline in inlines)
            {
                yield return inline;
                if (inline is Span span)
                    foreach (var inner in Elements(span.Inlines))
                        yield return inner;
            }
        }
    }

    static bool IsBold(TextElement e) => e.FontWeight.ToOpenTypeWeight() >= 600;
    static bool IsItalic(TextElement e) => e.FontStyle != FontStyles.Normal;

    // ---------- Plain-text view of one paragraph ----------

    /// <summary>The paragraph's text, with a soft line break (Shift+Enter) as '\n' and a picture as <see cref="PictureChar"/>.</summary>
    public static string PlainText(Paragraph p)
    {
        var sb = new StringBuilder();
        foreach (var item in Items(p.Inlines))
        {
            sb.Append(item switch
            {
                Run run => run.Text,
                LineBreak => "\n",
                _ => PictureChar.ToString(),
            });
        }
        return sb.ToString();
    }

    /// <summary>Number of characters of <paramref name="p"/> before <paramref name="position"/>.</summary>
    public static int CharOffset(Paragraph p, TextPointer position)
    {
        int count = 0;
        foreach (var item in Items(p.Inlines))
        {
            if (item is Run run)
            {
                if (position.CompareTo(run.ContentStart) < 0)
                    return count;
                if (position.CompareTo(run.ContentEnd) <= 0)
                    return count + run.ContentStart.GetOffsetToPosition(position);
                count += run.Text.Length;
            }
            else
            {
                if (position.CompareTo(item.ElementStart) <= 0)
                    return count;
                count++;
            }
        }
        return count;
    }

    /// <summary>The position after <paramref name="offset"/> characters of <paramref name="p"/>.</summary>
    public static TextPointer PositionAt(Paragraph p, int offset)
    {
        foreach (var item in Items(p.Inlines))
        {
            if (item is Run run)
            {
                if (offset <= run.Text.Length)
                    return run.ContentStart.GetPositionAtOffset(offset);
                offset -= run.Text.Length;
            }
            else
            {
                if (offset == 0)
                    return item.ElementStart;
                offset--;
            }
        }
        return p.ContentEnd;
    }

    /// <summary>Runs, line breaks and pictures in document order, looking inside spans.</summary>
    static IEnumerable<Inline> Items(InlineCollection inlines)
    {
        foreach (var inline in inlines)
        {
            if (inline is Run or LineBreak or InlineUIContainer)
                yield return inline;
            else if (inline is Span span)
                foreach (var inner in Items(span.Inlines))
                    yield return inner;
        }
    }
}
