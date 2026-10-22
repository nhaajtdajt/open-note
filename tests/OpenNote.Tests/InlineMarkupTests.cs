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
