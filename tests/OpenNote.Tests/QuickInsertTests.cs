using OpenNote.Core;

namespace OpenNote.Tests;

public class QuickInsertTests
{
    static string Apply(string text, TextEdit e) =>
        text[..e.Start] + e.Replacement + text[(e.Start + e.Length)..];

    [Theory]
    [InlineData("/", "-", "• ")]
    [InlineData("/", ">", "☐ ")]
    [InlineData("todo\r\n/", ">", "todo\r\n☐ ")]
    [InlineData("a /", "-", "a • ")]
    public void SlashShortcuts_Expand(string before, string typed, string after)
    {
        var edit = QuickInsert.TryExpand(before, before.Length, typed);
        Assert.NotNull(edit);
        Assert.Equal(after, Apply(before, edit.Value));
        Assert.Equal(after.Length, edit.Value.CaretAfter);
    }

    [Theory]
    [InlineData("", "-")]          // nothing before
    [InlineData("a", "-")]         // no slash
    [InlineData("/", "x")]         // not a shortcut key
    [InlineData("//", "-")]        // doubled slash stays literal
    [InlineData("/", "->")]        // pasted text, not a keystroke
    public void OtherInput_IsLeftAlone(string before, string typed) =>
        Assert.Null(QuickInsert.TryExpand(before, before.Length, typed));

    [Fact]
    public void Enter_OnBullet_ContinuesBullets()
    {
        var text = "• milk";
        var edit = Checklist.ContinueOnEnter(text, text.Length);
        Assert.NotNull(edit);
        Assert.Equal("• milk\r\n• ", Apply(text, edit.Value));
    }

    [Fact]
    public void Enter_OnEmptyBullet_EndsList()
    {
        var text = "• milk\r\n• ";
        Assert.Equal("• milk\r\n", Apply(text, Checklist.ContinueOnEnter(text, text.Length)!.Value));
    }

    [Fact]
    public void Bullet_IsNotAClickableBox_ButCtrlEnterTurnsItIntoOne()
    {
        var text = "• milk";
        Assert.False(Checklist.IsBoxAt(text, 0));
        Assert.Equal("☐ milk", Apply(text, Checklist.Toggle(text, 3)));
    }
}
