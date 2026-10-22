using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
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
    public static void Load(FlowDocument doc, string text, Func<string, InlineUIContainer>? createImage = null)
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
                paragraph.Inlines.Add(StyledRun(span.Text, span.Bold, span.Italic));
            doc.Blocks.Add(paragraph);
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
                        spans.Add(new StyledSpan(run.Text, IsBold(run), IsItalic(run)));
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
