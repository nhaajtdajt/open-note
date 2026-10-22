using System.Windows;
using System.Windows.Documents;
using OpenNote.Views;

namespace OpenNote.Tests;

/// <summary>Round trips between stored note text and the editor's FlowDocument (needs an STA thread, like WPF).</summary>
public class RichTextTests
{
    static T OnSta<T>(Func<T> work)
    {
        T result = default!;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { result = work(); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null)
            throw new AggregateException(error);
        return result;
    }

    [Theory]
    [InlineData("")]
    [InlineData("plain line")]
    [InlineData("Thứ Năm\r\n☐ Gửi CV\r\n☑ Ôn bài\r\n• Mua sữa")]
    [InlineData("a\r\n\r\n\r\nb\r\n")]                       // blank lines and a trailing newline survive
    [InlineData("tab\there and C:\\Users\\path")]
    [InlineData("**bold** and *italic* and ***both*** 2\\*3")]
    public void LoadThenSave_GivesBackTheSameText(string text) =>
        Assert.Equal(text, OnSta(() =>
        {
            var doc = new FlowDocument();
            RichText.Load(doc, text);
            return RichText.Save(doc);
        }));

    [Fact]
    public void Formatting_AppliedInTheEditor_IsSavedAsMarkers() =>
        Assert.Equal("say **hello** to *you*", OnSta(() =>
        {
            var doc = new FlowDocument();
            RichText.Load(doc, "say hello to you");
            var p = (Paragraph)doc.Blocks.FirstBlock;
            new TextRange(RichText.PositionAt(p, 4), RichText.PositionAt(p, 9)).ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold);
            new TextRange(RichText.PositionAt(p, 13), RichText.PositionAt(p, 16)).ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Italic);
            return RichText.Save(doc);
        }));

    [Fact]
    public void Offsets_MapAcrossStyledRuns() =>
        Assert.Equal(("ab**cd**ef", 3, 'd'), OnSta(() =>
        {
            var doc = new FlowDocument();
            RichText.Load(doc, "ab**cd**ef");
            var p = (Paragraph)doc.Blocks.FirstBlock;
            var at3 = RichText.PositionAt(p, 3);
            var next = at3.GetTextInRun(LogicalDirection.Forward)[0];
            return (RichText.Save(doc), RichText.CharOffset(p, at3), next);
        }));

    [Fact]
    public void PlainText_IsWhatListLogicSees() =>
        Assert.Equal("☐ buy milk", OnSta(() =>
        {
            var doc = new FlowDocument();
            RichText.Load(doc, "☐ buy **milk**");
            return RichText.PlainText((Paragraph)doc.Blocks.FirstBlock);
        }));
}
