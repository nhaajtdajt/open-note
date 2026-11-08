using System.Text;
using OpenNote.Core;

namespace OpenNote.Tests;

public sealed class NoteFileTests
{
    static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];
    static readonly byte[] Jpg = [0xFF, 0xD8, 0xFF, 0xE0, 9, 9];
    const string PngPath = "images/0123456789abcdef0123456789abcdef.png";
    const string JpgPath = "images/fedcba9876543210fedcba9876543210.jpg";

    [Fact]
    public void ToText_HasTitleThenPlainText()
    {
        var note = new Note { Heading = "Shopping", Text = "**milk** and *eggs*\r\n☐ {red}bread{/}\r\n" + ImageStore.Line(PngPath) };
        Assert.Equal("Shopping\r\n\r\nmilk and eggs\r\n☐ bread\r\n[image]\r\n", NoteFile.ToText(note));
    }

    [Fact]
    public void ToText_KeepsTheTitleAsTyped()
    {
        // Titles are plain text: their * and { aren't formatting marks.
        Assert.StartsWith("*important* {red}budget{/}\r\n", NoteFile.ToText(new Note { Heading = "*important* {red}budget{/}", Text = "x" }));
    }

    [Fact]
    public void WriteFile_ReplacesAnEarlierCopyAndLeavesNoTempFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), "OpenNoteTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "note.onote");
            NoteFile.WriteFile(path, [1, 2, 3]);
            NoteFile.WriteFile(path, [4, 5]);
            Assert.Equal([4, 5], File.ReadAllBytes(path));
            Assert.Equal(["note.onote"], Directory.GetFiles(dir).Select(Path.GetFileName));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ToText_WithoutTitle_IsJustTheText()
    {
        Assert.Equal("Ghi chú tiếng Việt\r\n", NoteFile.ToText(new Note { Text = "Ghi chú tiếng Việt" }));
    }

    [Fact]
    public void ToTextFile_IsUtf8WithoutBom()
    {
        var bytes = NoteFile.ToTextFile(new Note { Text = "Việt" });
        Assert.Equal("Việt\r\n", Encoding.UTF8.GetString(bytes));
        Assert.NotEqual(0xEF, bytes[0]);
    }

    [Fact]
    public void Onote_RoundTripsEverythingAndStoresPicturesAgain()
    {
        var note = new Note
        {
            Heading = "Trip", Text = $"**day 1**\r\n{ImageStore.Line(PngPath)}\r\n{{blue}}sea{{/}}\r\n{ImageStore.Line(JpgPath)}",
            Color = "#C3E4FF", Opacity = 0.6, FontSize = 18, Font = NoteFont.Hand, SpellCheck = false,
            IsImportant = true, Mark = "daisy", Width = 420, Height = 380, X = 5, Y = 6, IsLocked = true,
        };
        var file = NoteFile.ToOnote(note, path => path == PngPath ? Png : Jpg);

        var saved = new List<(byte[] Bytes, string Type)>();
        var copy = NoteFile.FromOnote(file, (bytes, type) =>
        {
            saved.Add((bytes, type));
            return $"images/{saved.Count:D32}.{type}";
        });

        Assert.Equal([(Png, "png"), (Jpg, "jpg")], saved.Select(s => (s.Bytes, s.Type)));
        Assert.Equal($"**day 1**\r\n{ImageStore.Line($"images/{1:D32}.png")}\r\n{{blue}}sea{{/}}\r\n{ImageStore.Line($"images/{2:D32}.jpg")}", copy.Text);
        Assert.Equal("Trip", copy.Heading);
        Assert.Equal("#C3E4FF", copy.Color);
        Assert.Equal(0.6, copy.Opacity);
        Assert.Equal(18, copy.FontSize);
        Assert.Equal(NoteFont.Hand, copy.Font);
        Assert.False(copy.SpellCheck);
        Assert.True(copy.IsImportant);
        Assert.Equal("daisy", copy.Mark);
        Assert.Equal((420, 380), (copy.Width, copy.Height));
        // A new note: its own id, placed where it opens, not locked.
        Assert.NotEqual(note.Id, copy.Id);
        Assert.Equal((0, 0), (copy.X, copy.Y));
        Assert.False(copy.IsLocked);
    }

    [Fact]
    public void Onote_MissingPictureIsLeftOut()
    {
        var note = new Note { Text = ImageStore.Line(PngPath) };
        var file = NoteFile.ToOnote(note, _ => null);
        var copy = NoteFile.FromOnote(file, (_, _) => throw new InvalidOperationException("nothing to save"));
        Assert.Equal(note.Text, copy.Text);
    }

    [Fact]
    public void FromOnote_RefusesOtherFiles()
    {
        Assert.Throws<InvalidDataException>(() => NoteFile.FromOnote(Encoding.UTF8.GetBytes("hello"), (_, _) => ""));
        Assert.Throws<InvalidDataException>(() => NoteFile.FromOnote(Encoding.UTF8.GetBytes("""{"version":1,"text":"x"}"""), (_, _) => ""));
        Assert.Throws<InvalidDataException>(() => NoteFile.FromOnote(Encoding.UTF8.GetBytes("""{"format":"Open Note","version":99}"""), (_, _) => ""));
    }

    [Fact]
    public void FromOnote_IgnoresUnsafeOrFakePictures()
    {
        // A path outside images/, a picture the text doesn't use, and data that isn't a picture.
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            format = "Open Note",
            version = 1,
            text = $"![image](../evil.png)\r\n![image]({PngPath})",
            pictures = new Dictionary<string, string>
            {
                ["../evil.png"] = Convert.ToBase64String(Png),
                [JpgPath] = Convert.ToBase64String(Jpg),
                [PngPath] = Convert.ToBase64String("not a png"u8.ToArray()),
            },
        });
        var copy = NoteFile.FromOnote(Encoding.UTF8.GetBytes(json), (_, _) => throw new InvalidOperationException("must not save"));
        Assert.Equal($"![image](../evil.png)\r\n![image]({PngPath})", copy.Text);
    }

    [Fact]
    public void FromOnote_ClampsOddValues()
    {
        var json = """{"format":"Open Note","version":1,"color":"pink","opacity":5,"fontSize":2,"mark":"nope","width":-3}""";
        var copy = NoteFile.FromOnote(Encoding.UTF8.GetBytes(json), (_, _) => "");
        Assert.Equal("#FFF1A6", copy.Color);
        Assert.Equal(1, copy.Opacity);
        Assert.Equal(9, copy.FontSize);
        Assert.Equal(NoteMarks.Star, copy.Mark);
        Assert.Equal(0, copy.Width);
    }

    [Fact]
    public void SuggestedName_DropsCharactersWindowsRefuses()
    {
        Assert.Equal("a b c", NoteFile.SuggestedName(new Note { Heading = "a/b:c" }));
        Assert.Equal("Note", NoteFile.SuggestedName(new Note()));
    }
}
