using OpenNote.Core;

namespace OpenNote.Tests;

public class SizeToggleTests
{
    static readonly PixelSize Normal = new(355, 330);
    static readonly PixelSize Big = new(900, 700);

    [Fact]
    public void BigNote_ShrinksToNormal_AndRemembersItsSize()
    {
        var (size, restore) = SizeToggle.Next(Big, null, Normal);
        Assert.Equal(Normal, size);
        Assert.Equal(Big, restore);
    }

    [Fact]
    public void SecondDoubleClick_RestoresTheLastSize_AndForgetsIt()
    {
        var (size, restore) = SizeToggle.Next(Normal, Big, Normal);
        Assert.Equal(Big, size);
        Assert.Null(restore);
    }

    [Fact]
    public void ThenAnotherDragAndDoubleClick_RemembersTheNewestSize()
    {
        var newer = new PixelSize(600, 500);
        var (shrunk, remembered) = SizeToggle.Next(newer, null, Normal);
        Assert.Equal((Normal, newer), (shrunk, remembered));
        Assert.Equal(newer, SizeToggle.Next(shrunk, remembered, Normal).Size);
    }

    [Theory]
    [InlineData(355, 330)]
    [InlineData(357, 328)]   // within a few pixels of normal counts as normal
    public void NoteAlreadyNormal_ChangesNothing(int w, int h)
    {
        var current = new PixelSize(w, h);
        var (size, restore) = SizeToggle.Next(current, null, Normal);
        Assert.Equal(current, size);
        Assert.Null(restore);
    }

    [Fact]
    public void SmallerThanNormal_AlsoGoesToNormal()
    {
        var small = new PixelSize(200, 150);
        var (size, restore) = SizeToggle.Next(small, null, Normal);
        Assert.Equal((Normal, small), (size, restore));
    }
}
