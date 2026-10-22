using OpenNote.Core;

namespace OpenNote.Tests;

public class ChecklistTests
{
    static string Apply(string text, TextEdit e) =>
        text[..e.Start] + e.Replacement + text[(e.Start + e.Length)..];

    [Fact]
    public void Toggle_PlainLine_BecomesOpenItem_AndCaretShifts()
    {
        var text = "first\r\nbuy milk";
        var edit = Checklist.Toggle(text, text.Length);
        Assert.Equal("first\r\n☐ buy milk", Apply(text, edit));
        Assert.Equal(text.Length + 2, edit.CaretAfter);
    }

    [Fact]
    public void Toggle_CyclesOpenAndDone()
    {
        var text = "☐ eggs";
        var done = Apply(text, Checklist.Toggle(text, 3));
        Assert.Equal("☑ eggs", done);
        Assert.Equal("☐ eggs", Apply(done, Checklist.Toggle(done, 3)));
    }

    [Fact]
    public void IsBoxAt_OnlyAtLineStart()
    {
        var text = "a ☐ b\r\n☐ c";
        Assert.False(Checklist.IsBoxAt(text, 2));
        Assert.True(Checklist.IsBoxAt(text, 7));
        Assert.False(Checklist.IsBoxAt(text, 8));
        Assert.False(Checklist.IsBoxAt(text, -1));
    }

    [Fact]
    public void Enter_OnItem_StartsNewItem()
    {
        var text = "☑ milk";
        var edit = Checklist.ContinueOnEnter(text, text.Length);
        Assert.NotNull(edit);
        Assert.Equal("☑ milk\r\n☐ ", Apply(text, edit.Value));
        Assert.Equal(text.Length + 4, edit.Value.CaretAfter);
    }

    [Fact]
    public void Enter_OnEmptyItem_EndsList()
    {
        var text = "☐ milk\r\n☐ ";
        var edit = Checklist.ContinueOnEnter(text, text.Length);
        Assert.NotNull(edit);
        Assert.Equal("☐ milk\r\n", Apply(text, edit.Value));
    }

    [Fact]
    public void Enter_OnPlainLine_IsNormal()
    {
        Assert.Null(Checklist.ContinueOnEnter("hello", 5));
        Assert.Null(Checklist.ContinueOnEnter("☐ x", 1)); // caret inside the box prefix
    }
}
