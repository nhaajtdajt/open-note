using OpenNote.Core;

namespace OpenNote.Tests;

public class AiContextTests
{
    [Fact]
    public void TitleAndTheWordsBefore()
    {
        Assert.Equal("Title: IELTS Writing\nJust before: Task 2: some people think that",
            AiContext.Build("  IELTS Writing ", "Task 2:\r\nsome people   think that "));
    }

    [Fact]
    public void OnlyTheLast30Words_AndNoListMarkers()
    {
        var before = string.Join(" ", Enumerable.Range(1, 40).Select(i => $"w{i}")) + "\r\n☐ ☑ • \n";
        var context = AiContext.Build("", before);
        Assert.Equal("Just before: … " + string.Join(" ", Enumerable.Range(11, 30).Select(i => $"w{i}")), context);
    }

    [Fact]
    public void NothingKnown_NoContext()
    {
        Assert.Equal("", AiContext.Build("", ""));
        Assert.Equal("", AiContext.Build("  ", "☐ \n"));
    }

    [Fact]
    public void Message_PutsContextFirst_MarkedAsContextOnly()
    {
        Assert.Equal("i goes", AiCorrector.UserMessage("i goes", ""));
        var message = AiCorrector.UserMessage("i goes", "Title: Lớp tiếng Anh");
        Assert.StartsWith("CONTEXT (only to understand the topic; do not check, translate or repeat it):\nTitle: Lớp tiếng Anh", message);
        Assert.EndsWith("\n\nTEXT:\ni goes", message);
    }
}
