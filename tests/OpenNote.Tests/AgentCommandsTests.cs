using OpenNote.Core;

namespace OpenNote.Tests;

/// <summary>The opennote-ai command, run against notes in memory instead of the app.</summary>
public class AgentCommandsTests
{
    sealed class FakeHost : IAgentNotes
    {
        public List<Note> List { get; } = [];
        public IReadOnlyList<Note> Notes => List;
        public AppSettings Settings { get; } = new() { NativeLanguage = "vi" };
        public List<string> SavedPictures { get; } = [];
        public string? PicturePath(string relativePath) => @"C:\data\" + relativePath.Replace('/', '\\');
        public string SavePicture(string file)
        {
            SavedPictures.Add(file);
            return $"images/{Guid.NewGuid():N}.png";
        }
        public Note Create(string heading, string text, string color)
        {
            var note = new Note { Heading = heading, Text = text, Color = color };
            List.Add(note);
            return note;
        }
        public void Append(Note note, string text) => note.Text += "\r\n" + text;
        public string? SaveStyle(string style)
        {
            Settings.NoteStyle = style;
            return null;
        }
    }

    static FakeHost WithNotes()
    {
        var host = new FakeHost();
        host.List.Add(new Note { Heading = "Đi chợ", Text = "☐ Mua sữa\r\n☐ Mua trứng", ModifiedUtc = DateTime.UtcNow.AddHours(-2) });
        host.List.Add(new Note { Heading = "Task abc", Text = "• Hạn **thứ Sáu**", ModifiedUtc = DateTime.UtcNow, IsImportant = true });
        host.List.Add(new Note { Heading = "Cũ", Text = "đã xong", State = NoteState.Archived });
        return host;
    }

    static (int Code, string Output) Run(FakeHost host, params string[] args) => AgentCommands.Run(args, Path.GetTempPath(), host);

    static string Id(Note n) => n.Id.ToString("N")[..8];

    [Fact]
    public void NoArguments_PrintsTheGuide_WithTheStyleAndLanguage()
    {
        var host = WithNotes();
        var (code, output) = Run(host);
        Assert.Equal(0, code);
        Assert.Contains("opennote-ai <command>", output);
        Assert.Contains(AgentCommands.DefaultStyle, output);
        Assert.Contains("Vietnamese", output);

        host.Settings.NoteStyle = "Mỗi ý có giải thích ngắn.";
        Assert.Contains("Mỗi ý có giải thích ngắn.", Run(host, "help").Output);
    }

    [Fact]
    public void List_ShowsNotArchivedNotes_NewestFirst()
    {
        var host = WithNotes();
        var lines = Run(host, "list").Output.Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.StartsWith(Id(host.List[1]) + "  \"Task abc\"", lines[0]);
        Assert.Contains("important", lines[0]);
        Assert.Contains("\"Cũ\"", Run(host, "list", "--archived").Output);
        Assert.Equal(3, Run(host, "list", "--all").Output.Split('\n').Length);
    }

    [Fact]
    public void Search_IgnoresAccents_AndIncludesTheArchive()
    {
        var host = WithNotes();
        Assert.Contains("\"Đi chợ\"", Run(host, "search", "mua", "sua").Output);
        Assert.Contains("\"Cũ\"", Run(host, "search", "da xong").Output);
        Assert.Contains("No note contains", Run(host, "search", "zzz").Output);
    }

    [Fact]
    public void Read_ByIdOrTitle_GivesTheTextWithMarkupAndFullPicturePaths()
    {
        var host = WithNotes();
        var abc = host.List[1];
        abc.Text += "\r\n![image](images/0123456789abcdef0123456789abcdef.png)";
        var byId = Run(host, "read", Id(abc));
        Assert.Equal(0, byId.Code);
        Assert.Contains("• Hạn **thứ Sáu**", byId.Output);
        Assert.Contains(@"![image](C:\data\images\0123456789abcdef0123456789abcdef.png)", byId.Output);
        Assert.Equal(byId.Output, Run(host, "read", "task ABC").Output);
        Assert.Equal(3, Run(host, "read", "--all").Output.Split("=== ").Length - 1);
    }

    [Fact]
    public void Read_UnknownOrAmbiguous_SaysWhatToDo()
    {
        var host = WithNotes();
        host.List.Add(new Note { Heading = "Task abc" });
        var (code, output) = Run(host, "read", "Task abc");
        Assert.Equal(2, code);
        Assert.Contains("matches several notes", output);
        Assert.Contains("No note", Run(host, "read", "nothing").Output);
    }

    [Fact]
    public void New_TurnsMarkdownListsIntoNoteLines_AndAddsPicturesAtTheEnd()
    {
        var host = new FakeHost();
        var picture = Path.Combine(Path.GetTempPath(), $"agent-test-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(picture, [1, 2, 3]);
        try
        {
            var (code, output) = Run(host, "new", "--title", "Kế hoạch", "--text", "- [ ] Gửi CV\n- [x] Ôn bài\n- Họp 9h", "--image", picture, "--color", "index card");
            Assert.Equal(0, code);
            Assert.Contains("Created note", output);
            var note = Assert.Single(host.List);
            Assert.Equal("Kế hoạch", note.Heading);
            Assert.Equal(Palette.Colors.Single(c => c.Name == "Index card").Hex, note.Color);
            var lines = note.Text.Split("\r\n");
            Assert.Equal(["☐ Gửi CV", "☑ Ôn bài", "• Họp 9h"], lines[..3]);
            Assert.True(ImageStore.IsImageLine(lines[3], out _));
            Assert.Equal([picture], host.SavedPictures);
        }
        finally
        {
            File.Delete(picture);
        }
    }

    [Fact]
    public void New_RejectsBadInput_WithoutCreatingANote()
    {
        var host = new FakeHost();
        Assert.Equal(2, Run(host, "new").Code);
        Assert.Equal(2, Run(host, "new", "--title", "x", "--image", "missing.png").Code);
        Assert.Equal(2, Run(host, "new", "--title", "x", "--color", "neon").Code);
        Assert.Equal(2, Run(host, "new", "--title").Code);
        Assert.Equal(2, Run(host, "new", "--title", "x", "--bogus").Code);
        Assert.Empty(host.List);
        Assert.Empty(host.SavedPictures);
    }

    [Fact]
    public void Add_AppendsToANote_ButNeverToALockedOne()
    {
        var host = WithNotes();
        var shop = host.List[0];
        Assert.Equal(0, Run(host, "add", Id(shop), "--text", "- [ ] Mua bánh").Code);
        Assert.EndsWith("\r\n☐ Mua bánh", shop.Text);

        shop.IsLocked = true;
        var (code, output) = Run(host, "add", Id(shop), "--text", "x");
        Assert.Equal(2, code);
        Assert.Contains("locked", output);
        Assert.DoesNotContain("x", shop.Text.Split("\r\n")[^1]);
    }

    [Fact]
    public void Style_ShowsSetsAndClears()
    {
        var host = new FakeHost();
        Assert.Equal(AgentCommands.DefaultStyle, Run(host, "style").Output);
        Assert.Equal(0, Run(host, "style", "--set", "Ngắn gọn.\r\nCó giải thích sau dấu —.").Code);
        Assert.Equal("Ngắn gọn.\nCó giải thích sau dấu —.", host.Settings.NoteStyle);
        Assert.Equal(host.Settings.NoteStyle, Run(host, "style").Output);
        Run(host, "style", "--clear");
        Assert.Equal("", host.Settings.NoteStyle);
        Assert.Equal(2, Run(host, "style", "--set", new string('x', 5000)).Code);
    }

    [Fact]
    public void TurnedOff_OnlyTheGuideWorks_AndItSaysHowToTurnItOn()
    {
        var host = WithNotes();
        host.Settings.AllowAgents = false;
        var (code, output) = Run(host, "list");
        Assert.Equal(3, code);
        Assert.Contains("turned off", output);
        Assert.Equal(3, Run(host, "new", "--title", "x").Code);
        Assert.Contains("turned off", Run(host).Output);
        Assert.Equal(3, host.List.Count);
    }

    [Fact]
    public void Titles_AreShownAsTyped_EvenWithAsterisks()
    {
        var host = new FakeHost();
        host.List.Add(new Note { Heading = "5*3*2 math" });
        Assert.Contains("\"5*3*2 math\"", Run(host, "list").Output);
        Assert.Equal(0, Run(host, "read", "5*3*2 math").Code);
    }

    [Fact]
    public void New_RefusesAHugeFile_BeforeReadingIt()
    {
        var host = new FakeHost();
        var big = Path.Combine(Path.GetTempPath(), $"agent-big-{Guid.NewGuid():N}.txt");
        File.WriteAllText(big, new string('a', 400_000));
        try
        {
            var (code, output) = Run(host, "new", "--title", "x", "--file", big);
            Assert.Equal(2, code);
            Assert.Contains("too big", output);
            Assert.Empty(host.List);
        }
        finally
        {
            File.Delete(big);
        }
    }

    [Fact]
    public void DataFolder_WithOrWithoutTrailingBackslash_IsOneRunningCopy()
    {
        var plain = OpenNote.Program.ParseDataDirectory(["--data", @"C:\notes"])!;
        var slash = OpenNote.Program.ParseDataDirectory(["--data", @"C:\notes\"])!;
        Assert.Equal(plain, slash);
        Assert.Equal(OpenNote.Program.InstanceId(plain), OpenNote.Program.InstanceId(slash));
        Assert.Equal(@"C:\", OpenNote.Program.ParseDataDirectory(["--data", @"C:\"]));
    }

    [Fact]
    public void UnknownCommand_PointsToTheGuide()
    {
        var (code, output) = Run(WithNotes(), "delete", "abc");
        Assert.Equal(2, code);
        Assert.Contains("Run opennote-ai", output);
    }

    [Theory]
    [InlineData("a\nb", "a\r\nb")]
    [InlineData("  - [ ] indented", "  ☐ indented")]
    [InlineData("* bullet\r\n2*3 stays", "• bullet\r\n2*3 stays")]
    [InlineData("**bold** line\n\n", "**bold** line")]
    public void NormalizeText(string input, string expected) => Assert.Equal(expected, AgentCommands.NormalizeText(input));
}
