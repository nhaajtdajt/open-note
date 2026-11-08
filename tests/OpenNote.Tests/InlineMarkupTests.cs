using OpenNote.Core;

namespace OpenNote.Tests;

public class InlineMarkupTests
{
    static StyledSpan P(string t) => new(t, false, false);
    static StyledSpan B(string t) => new(t, true, false);
    static StyledSpan I(string t) => new(t, false, true);
    static StyledSpan BI(string t) => new(t, true, true);

    [Fact]
    public void Parses_BoldItalicAndBoth()
    {
        Assert.Equal([P("a "), B("bold"), P(" b "), I("it"), P(" "), BI("both")],
            InlineMarkup.Parse("a **bold** b *it* ***both***"));
    }

    [Theory]
    [InlineData("2 * 3 = 6")]        // spaced asterisks are not emphasis
    [InlineData("* list item")]
    [InlineData("**unclosed")]
    [InlineData("a ** b")]
    public void LeavesLonelyAsterisksAlone(string text)
    {
        Assert.Equal(text, InlineMarkup.Strip(text));
        Assert.All(InlineMarkup.Parse(text), s => Assert.False(s.Bold || s.Italic));
    }

    [Fact]
    public void ManyUnclosedColorTags_ParseQuicklyAsPlainText()
    {
        // 100,000 "{red}" with no "{/}" (a crafted note file) used to take billions of steps.
        var text = string.Concat(Enumerable.Repeat("{red}", 100_000));
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var spans = InlineMarkup.Parse(text);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), $"took {watch.Elapsed}");
        Assert.Equal(text, string.Concat(spans.Select(s => s.Text)));
        Assert.All(spans, s => Assert.Null(s.Color));
        // A closed tag later on still colors as before.
        Assert.Equal([P("a "), new StyledSpan("b", false, false, "red")], InlineMarkup.Parse("a {red}b{/}"));
    }

    [Fact]
    public void Write_RoundTripsThroughParse()
    {
        StyledSpan[] spans = [P("Ghi "), B("chú"), P(" có "), I("nghiêng"), P(" và "), BI("cả hai"), P(" 2*3")];
        var text = InlineMarkup.Write(spans);
        Assert.Equal("Ghi **chú** có *nghiêng* và ***cả hai*** 2\\*3", text);
        Assert.Equal(spans, InlineMarkup.Parse(text));
    }

    [Fact]
    public void Backslashes_NextToMarkersAndAsterisks_RoundTrip()
    {
        StyledSpan[][] cases =
        [
            [P("path\\"), B("x")],
            [B("x\\"), P(" y")],
            [P("a\\*b "), I("c\\\\")],
            [P("\\\\server\\share "), B("*star*")],
            [P("ends with \\")],
        ];
        foreach (var spans in cases)
            Assert.Equal(spans, InlineMarkup.Parse(InlineMarkup.Write(spans)));
    }

    [Fact]
    public void OrdinaryBackslashes_AreStoredAsTyped()
    {
        Assert.Equal(@"C:\Users\me and **bold**", InlineMarkup.Write([P(@"C:\Users\me and "), B("bold")]));
        Assert.Equal(@"C:\Users\me", InlineMarkup.Strip(@"C:\Users\me"));
    }

    [Fact]
    public void Write_MergesNeighbours_AndKeepsSpacesOutsideMarkers()
    {
        Assert.Equal("say **hello world** now", InlineMarkup.Write([P("say"), B(" hello"), B(" world "), P("now")]));
    }

    [Fact]
    public void TextColor_IsStoredAsTags_AroundBoldAndItalic()
    {
        StyledSpan[] spans = [P("a "), new("red", false, false, "red"), new(" bold", true, false, "red"), P(" "), new("blue", false, true, "blue")];
        var text = InlineMarkup.Write(spans);
        Assert.Equal("a {red}red **bold**{/} {blue}*blue*{/}", text);
        // The space before "bold" moves outside the bold markers but stays red.
        StyledSpan[] read = [P("a "), new("red ", false, false, "red"), new("bold", true, false, "red"), P(" "), new("blue", false, true, "blue")];
        Assert.Equal(read, InlineMarkup.Parse(text));
        Assert.Equal("a red bold blue", InlineMarkup.Strip(text));
    }

    [Theory]
    [InlineData("a {red} b")]                 // no closing tag: plain text
    [InlineData("set {x} and {/} and {pink}y{/}")] // not a color tag
    [InlineData(@"C:\path\{x} and \{ }")]
    public void TextColor_UnclosedOrUnknownTags_StayText(string text) =>
        Assert.Equal(text, InlineMarkup.Strip(text));

    [Fact]
    public void TextColor_LiteralTagsAndBackslashes_RoundTrip()
    {
        StyledSpan[][] cases =
        [
            [P("type {red}x{/} to color")],
            [P("dir\\"), new("x", false, false, "green")],
            [new("end\\", false, false, "red"), P("after")],
            [new("{/} inside", false, false, "purple")],
            [P(@"old \{ brace \\{red}")],
        ];
        foreach (var spans in cases)
            Assert.Equal(spans, InlineMarkup.Parse(InlineMarkup.Write(spans)));
    }

    [Fact]
    public void AdjacentStylesReadBackCorrectly()
    {
        StyledSpan[] spans = [B("a"), I("b")];
        Assert.Equal(spans, InlineMarkup.Parse(InlineMarkup.Write(spans)));
        StyledSpan[] reversed = [I("a"), B("b")];
        Assert.Equal(reversed, InlineMarkup.Parse(InlineMarkup.Write(reversed)));
    }

    [Fact]
    public void Strip_RemovesMarkers_ForTitlesAndSearch()
    {
        var note = new Note { Text = "**Họp nhóm** lúc *7h*" };
        Assert.Equal("Họp nhóm lúc 7h", note.Title);
        Assert.True(NoteSearch.Matches(note, "nhom luc 7h"));
    }

    [Theory]
    [InlineData("xin chào các bạn", true)]
    [InlineData("Đi học", true)]
    [InlineData("I love the café", false)]
    [InlineData("WS_EX_TOOLWINDOW, L\"MyWindowClass\"", false)]
    [InlineData("résumé déjà", true)] // two shared accents: treated as non-English, spell check stays off
    public void DetectsVietnamese(string text, bool expected) =>
        Assert.Equal(expected, LanguageHint.LooksVietnamese(text));
}
