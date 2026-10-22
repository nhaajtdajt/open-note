using OpenNote.Core;

namespace OpenNote.Tests;

public class HotkeyAndSearchTests
{
    [Theory]
    [InlineData("Ctrl+Alt+N", HotkeyGesture.ModControl | HotkeyGesture.ModAlt, 0x4E)]
    [InlineData("win + shift + f5", HotkeyGesture.ModWin | HotkeyGesture.ModShift, 0x74)]
    [InlineData("Ctrl+Space", HotkeyGesture.ModControl, 0x20)]
    [InlineData("Alt+1", HotkeyGesture.ModAlt, 0x31)]
    public void Parses(string text, uint mods, uint vk)
    {
        Assert.True(HotkeyGesture.TryParse(text, out var g));
        Assert.Equal(new HotkeyGesture(mods, vk), g);
    }

    [Theory]
    [InlineData("")]
    [InlineData("N")]              // no modifier: would steal a normal key
    [InlineData("Ctrl+Ctrl+N")]
    [InlineData("Hyper+N")]
    [InlineData("Ctrl+F99")]
    [InlineData("Ctrl+Alt")]
    public void Rejects(string text) => Assert.False(HotkeyGesture.TryParse(text, out _));

    [Theory]
    [InlineData("Ghi chú họp", "ghi chu", true)]
    [InlineData("Buy MILK and eggs", "eggs milk", true)]
    [InlineData("Buy milk", "bread", false)]
    [InlineData("anything", "  ", true)]
    public void Search(string text, string query, bool expected) =>
        Assert.Equal(expected, NoteSearch.Matches(text, query));

    [Fact]
    public void Title_AndPreview_SkipBlankLines()
    {
        var note = new Note { Text = "\r\n  Shopping \r\n\r\n☐ milk\r\n☐ eggs" };
        Assert.Equal("Shopping", note.Title);
        Assert.Equal("☐ milk  /  ☐ eggs", note.Preview);
    }

    [Fact]
    public void Palette_FallsBackAndDetectsDark()
    {
        Assert.Equal(Palette.ParseRgb(Palette.Default.Hex), Palette.ParseRgb("not a color"));
        Assert.True(Palette.IsDark("#3A3F47"));
        Assert.False(Palette.IsDark(Palette.Default.Hex));
        Assert.Equal(Palette.ParseRgb(Palette.Default.StripHex), Palette.Strip(Palette.Default.Hex));
        Assert.Equal(((byte)255, (byte)255, (byte)255), Palette.Mix((10, 20, 30), (255, 255, 255), 2));
    }
}
