using System.Globalization;
using System.IO;
using System.Text;

namespace OpenNote.Core;

/// <summary>What the opennote-ai command needs from the running app.</summary>
public interface IAgentNotes
{
    IReadOnlyList<Note> Notes { get; }
    AppSettings Settings { get; }

    /// <summary>Absolute path of a picture a note refers to (images/….png), or null.</summary>
    string? PicturePath(string relativePath);

    /// <summary>Stores a picture file and returns its path for the note text; throws if it isn't a picture.</summary>
    string SavePicture(string file);

    /// <summary>Creates a note and shows it, without taking the keyboard away from the user.</summary>
    Note Create(string heading, string text, string color);

    /// <summary>Adds lines at the end of a note (its window shows them at once).</summary>
    void Append(Note note, string text);

    /// <summary>Saves the user's note style; returns an error message, or null.</summary>
    string? SaveStyle(string style);
}

/// <summary>A command the user or the agent got wrong; the message says how to fix it.</summary>
public sealed class AgentException(string message) : Exception(message);

/// <summary>
/// The opennote-ai command: lets an AI agent on this PC (Claude Code, Codex, Cursor...) read the
/// user's notes and add new ones. Running it with no arguments prints a guide, so a prompt like
/// "run opennote-ai to see how to use it" is all an agent needs. There is no command that deletes
/// or rewrites a note: an agent can only read, create, and add at the end.
/// </summary>
public static class AgentCommands
{
    public const string Command = "opennote-ai";
    const int MaxText = 100_000, MaxStyle = 4_000, MaxPictures = 10;
    const long MaxPictureBytes = 20_000_000;
    static readonly string[] PictureExtensions = [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tif", ".tiff", ".webp"];

    /// <summary>How notes are written when the user hasn't described their own style: short, like a real sticky note.</summary>
    public const string DefaultStyle =
        "Write each note like a sticky note, not a document:\n" +
        "- Title: 2 to 5 words that say what the note is about.\n" +
        "- One topic per note, at most about 8 lines and 60 words. If there is more, keep what matters most or split it into a few notes.\n" +
        "- One idea per line, no paragraphs. The most important line first.\n" +
        "- Tasks as \"☐ \" lines that start with a verb; facts as \"• \" lines.\n" +
        "- Keep exact dates, times, numbers, names and links.\n" +
        "- Add a short reason or detail after \" — \" only when it really helps.\n" +
        "- Bold only the key word; red only for deadlines and warnings.\n" +
        "- No greetings, no filler, no repeating the title.";

    public static (int Code, string Output) Run(IReadOnlyList<string> args, string workingDirectory, IAgentNotes host)
    {
        try
        {
            if (args.Count == 0 || args[0] is "help" or "--help" or "-h" or "/?")
                return (0, Guide(host));
            if (!host.Settings.AllowAgents)
                return (3, Off(host));
            var a = new Args(args);
            return a.Verb switch
            {
                "list" => List(host, a),
                "search" => Search(host, a),
                "read" => Read(host, a),
                "new" => New(host, a, workingDirectory),
                "add" => Add(host, a, workingDirectory),
                "style" => Style(host, a, workingDirectory),
                _ => throw new AgentException($"Unknown command \"{a.Verb}\". Run {Command} to see the commands."),
            };
        }
        catch (AgentException ex)
        {
            return (2, ex.Message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (1, "Open Note couldn't do that: " + ex.Message);
        }
    }

    // ---------- The guide ----------

    public static string Guide(IAgentNotes host)
    {
        var s = host.Settings;
        var sb = new StringBuilder();
        sb.AppendLine("Open Note: sticky notes on this Windows PC. This command lets you read the user's notes and add new ones.");
        sb.AppendLine("A note you create shows up on the user's screen right away.");
        if (!s.AllowAgents)
            sb.AppendLine().AppendLine(Off(host));
        sb.AppendLine();
        sb.AppendLine($"Commands (run as: {Command} <command>):");
        sb.AppendLine("  list [--archived | --all]       Notes on screen and closed ones, newest first (--archived: the archive; --all: everything).");
        sb.AppendLine("  search <words>                  Notes containing every word, archive included (case and accents ignored).");
        sb.AppendLine("  read <note>... | read --all     Full text. <note> is the 8-character id from list/search, or the exact title.");
        sb.AppendLine("  new --title <title> --text <text> [--image <file>]... [--color <color>]");
        sb.AppendLine("                                  Creates a note and shows it.");
        sb.AppendLine("  add <note> --text <text> [--image <file>]...");
        sb.AppendLine("                                  Adds lines at the end of a note (not a locked one).");
        sb.AppendLine("  style                           The user's note style (also below).");
        sb.AppendLine("  style --set <text>              Saves the user's note style. Only when the user asks, for example to learn");
        sb.AppendLine("                                  it: read --all, describe how they write in a few short lines, then save it.");
        sb.AppendLine("  For long text use --file <path> (UTF-8) or --text - (read from stdin) instead of --text <text>.");
        sb.AppendLine("  There is no command to delete or rewrite a note, on purpose.");
        sb.AppendLine();
        sb.AppendLine("How note text works:");
        sb.AppendLine("  - Plain text, one line per item. \"☐ \" (U+2610 and a space) starts a task the user can tick; \"• \" (U+2022) a bullet.");
        sb.AppendLine("    Markdown \"- [ ] \", \"- [x] \" and \"- \" lines are turned into these for you.");
        sb.AppendLine("  - **bold**, *italic*, and colored text: {red}…{/} (also {orange}, {green}, {blue}, {purple}).");
        sb.AppendLine("  - Pictures: --image adds a picture file (png, jpg, gif, bmp, tif, webp) on its own line at the end.");
        sb.AppendLine("  - Notes are small windows on the desktop: keep them short. Several short notes beat one long one.");
        sb.AppendLine($"  - --color: {string.Join(", ", Palette.Colors.Select(c => c.Name))} (default: the user's usual color).");
        sb.AppendLine("  - Output is UTF-8. In Windows PowerShell 5.1, if letters look wrong, first run:");
        sb.AppendLine("    [Console]::OutputEncoding = [Text.UTF8Encoding]::new()");
        sb.AppendLine();
        sb.AppendLine(string.IsNullOrWhiteSpace(s.NoteStyle)
            ? "The user's note style (Open Note's default; they haven't written their own). Follow it when you write notes:"
            : "The user's note style, in their own words. Follow it when you write notes:");
        sb.AppendLine(CurrentStyle(s));
        sb.AppendLine();
        sb.Append($"The user's language: {s.Native().EnglishName}. Write notes in the language the user writes to you in.");
        return sb.ToString();
    }

    static string Off(IAgentNotes host) =>
        $"The user has turned off AI agents in Open Note, so no command works. To turn them on: {host.Settings.HelpHotkey} → AI agents → \"Let AI agents read and add notes\".";

    static string CurrentStyle(AppSettings s) => string.IsNullOrWhiteSpace(s.NoteStyle) ? DefaultStyle : s.NoteStyle.Trim();

    // ---------- Reading ----------

    static (int, string) List(IAgentNotes host, Args a)
    {
        a.NoWords();
        var notes = host.Notes.Where(n => a.Has("--all") || (n.State == NoteState.Archived) == a.Has("--archived"));
        return (0, Lines(notes, a.Has("--archived") ? "The archive is empty." : "No notes."));
    }

    static (int, string) Search(IAgentNotes host, Args a)
    {
        if (a.Words.Count == 0)
            throw new AgentException($"What to look for? Example: {Command} search milk");
        var query = string.Join(' ', a.Words);
        return (0, Lines(host.Notes.Where(n => NoteSearch.Matches(n, query)), $"No note contains \"{query}\"."));
    }

    static (int, string) Read(IAgentNotes host, Args a)
    {
        var notes = a.Has("--all")
            ? Newest(host.Notes).ToList()
            : a.Words.Count > 0
                ? a.Words.Select(w => Find(host, w)).Distinct().ToList()
                : throw new AgentException($"Which note? Give its id or title (see {Command} list), or --all.");
        if (notes.Count == 0)
            return (0, "No notes.");
        var sb = new StringBuilder();
        foreach (var n in notes)
        {
            if (sb.Length > 0)
                sb.AppendLine().AppendLine();
            sb.AppendLine($"=== {Describe(n)}");
            foreach (var line in n.Text.Split('\n'))
            {
                var text = line.TrimEnd('\r');
                sb.AppendLine(ImageStore.IsImageLine(text, out var path) ? ImageStore.Line(host.PicturePath(path) ?? path) : text);
            }
        }
        return (0, sb.ToString().TrimEnd());
    }

    static IEnumerable<Note> Newest(IEnumerable<Note> notes) => notes.OrderByDescending(n => n.ModifiedUtc);

    static string Lines(IEnumerable<Note> notes, string none)
    {
        var list = Newest(notes).ToList();
        return list.Count == 0 ? none : string.Join("\n", list.Select(Describe));
    }

    /// <summary>"3f2a9c1b  "Title"  (on screen, edited 2026-11-07 17:52, important)".</summary>
    static string Describe(Note n)
    {
        var facts = new List<string>
        {
            n.State switch { NoteState.Visible => "on screen", NoteState.Archived => "archived", _ => "closed" },
            "edited " + n.ModifiedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
        };
        if (n.IsImportant)
            facts.Add("important");
        if (n.IsLocked)
            facts.Add("locked");
        return $"{ShortId(n)}  \"{n.Title}\"  ({string.Join(", ", facts)})";
    }

    static string ShortId(Note n) => n.Id.ToString("N")[..8];

    /// <summary>A note by id (8 characters, or at least 4) or by its exact title (case and accents ignored).</summary>
    static Note Find(IAgentNotes host, string key)
    {
        var k = key.Trim();
        var byId = k.Length >= 4 ? host.Notes.Where(n => n.Id.ToString("N").StartsWith(k, StringComparison.OrdinalIgnoreCase)).ToList() : [];
        if (byId.Count == 1)
            return byId[0];
        var byTitle = host.Notes.Where(n => string.Compare(n.Title, k, CultureInfo.InvariantCulture,
            CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) == 0).ToList();
        if (byTitle.Count == 1)
            return byTitle[0];
        var several = byId.Concat(byTitle).Distinct().ToList();
        if (several.Count > 1)
            throw new AgentException($"\"{key}\" matches several notes; use the id:\n" + Lines(several, ""));
        throw new AgentException($"No note \"{key}\". Use {Command} list or {Command} search to find its id.");
    }

    // ---------- Writing ----------

    static (int, string) New(IAgentNotes host, Args a, string cwd)
    {
        var title = (a.One("--title") ?? string.Join(' ', a.Words)).Trim();
        var text = Body(a, cwd);
        var color = a.One("--color") is { } name ? NoteColor(name) : host.Settings.DefaultColor;
        if (title.Length == 0 && text.Length == 0 && a.All("--image").Count == 0)
            throw new AgentException("Nothing to write: give --title, --text, --file or --image.");
        var pictures = SavePictures(host, a, cwd);
        var note = host.Create(title, Join(text, pictures), color);
        return (0, $"Created note {ShortId(note)} \"{note.Title}\". It is on the user's screen now.");
    }

    static (int, string) Add(IAgentNotes host, Args a, string cwd)
    {
        if (a.Words.Count != 1)
            throw new AgentException($"Which note? Example: {Command} add 3f2a9c1b --text \"☐ Call Minh\"");
        var note = Find(host, a.Words[0]);
        if (note.IsLocked)
            throw new AgentException($"\"{note.Title}\" is locked: the user doesn't want it changed. Create a new note instead.");
        var text = Body(a, cwd);
        if (text.Length == 0 && a.All("--image").Count == 0)
            throw new AgentException("Nothing to add: give --text, --file or --image.");
        var pictures = SavePictures(host, a, cwd);
        host.Append(note, Join(text, pictures));
        return (0, $"Added to note {ShortId(note)} \"{note.Title}\".");
    }

    static (int, string) Style(IAgentNotes host, Args a, string cwd)
    {
        a.NoWords();
        string? style = a.Has("--clear") ? "" : a.One("--set") ?? (a.One("--file") is { } file ? ReadFile(file, cwd) : null);
        if (style is null)
            return (0, CurrentStyle(host.Settings));
        style = style.Replace("\r\n", "\n").Trim();
        if (style.Length > MaxStyle)
            throw new AgentException($"The style is {style.Length} characters; keep it under {MaxStyle} (a few short lines).");
        if (host.SaveStyle(style) is { } error)
            throw new AgentException(error);
        return (0, style.Length == 0 ? "Cleared: Open Note's default style is used again." : "Saved the user's note style.");
    }

    /// <summary>The text to write, from --text or --file, in the note's own format.</summary>
    static string Body(Args a, string cwd)
    {
        var text = a.One("--text") ?? (a.One("--file") is { } file ? ReadFile(file, cwd) : "");
        if (text.Length > MaxText)
            throw new AgentException($"The text is {text.Length} characters; a note holds at most {MaxText}. Split it into several notes.");
        return NormalizeText(text);
    }

    static string ReadFile(string name, string cwd)
    {
        var path = Path.GetFullPath(name, cwd.Length > 0 ? cwd : Environment.CurrentDirectory);
        var file = new FileInfo(path);
        if (!file.Exists)
            throw new AgentException($"No file {path}.");
        // Checked before reading: a big log or binary must not freeze the notes (this runs on the UI thread).
        if (file.Length > 3L * MaxText + 3)
            throw new AgentException($"{file.Name} is too big for a note ({file.Length / 1024} KB); a note holds at most {MaxText} characters.");
        return File.ReadAllText(path, Encoding.UTF8);
    }

    static List<string> SavePictures(IAgentNotes host, Args a, string cwd)
    {
        var files = a.All("--image");
        if (files.Count > MaxPictures)
            throw new AgentException($"At most {MaxPictures} pictures at a time.");
        // Check them all before storing any, so a bad one doesn't leave half a note.
        var paths = files.Select(f => Path.GetFullPath(f, cwd.Length > 0 ? cwd : Environment.CurrentDirectory)).ToList();
        foreach (var path in paths)
        {
            var info = new FileInfo(path);
            if (!info.Exists)
                throw new AgentException($"No picture file {path}.");
            if (!PictureExtensions.Contains(info.Extension.ToLowerInvariant()))
                throw new AgentException($"{info.Name} isn't a picture Open Note can show (png, jpg, gif, bmp, tif, webp).");
            if (info.Length > MaxPictureBytes)
                throw new AgentException($"{info.Name} is larger than 20 MB.");
        }
        var saved = new List<string>();
        foreach (var path in paths)
        {
            try
            {
                saved.Add(host.SavePicture(path));
            }
            catch (Exception ex) when (ex is NotSupportedException or ArgumentException or FileFormatException or InvalidOperationException)
            {
                throw new AgentException($"{Path.GetFileName(path)} couldn't be read as a picture.");
            }
        }
        return saved;
    }

    static string Join(string text, List<string> pictures) =>
        string.Join("\r\n", (text.Length > 0 ? [text] : Array.Empty<string>()).Concat(pictures.Select(ImageStore.Line)));

    static string NoteColor(string name)
    {
        var wanted = name.Replace(" ", "").Replace("-", "");
        return Palette.Colors.FirstOrDefault(c => string.Equals(c.Name.Replace(" ", ""), wanted, StringComparison.OrdinalIgnoreCase))?.Hex
            ?? throw new AgentException($"No color \"{name}\". Colors: {string.Join(", ", Palette.Colors.Select(c => c.Name))}.");
    }

    /// <summary>
    /// Agents write Markdown lists; notes use their own "☐ " / "☑ " / "• " lines (which the user
    /// can tick and the app understands), and CRLF line ends.
    /// </summary>
    public static string NormalizeText(string text)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd();
            var body = line.TrimStart();
            var indent = line[..(line.Length - body.Length)];
            if (body.StartsWith("- [ ] ") || body.StartsWith("* [ ] "))
                line = indent + "☐ " + body[6..];
            else if (body.StartsWith("- [x] ", StringComparison.OrdinalIgnoreCase) || body.StartsWith("* [x] ", StringComparison.OrdinalIgnoreCase))
                line = indent + "☑ " + body[6..];
            else if (body.StartsWith("- ") || body.StartsWith("* "))
                line = indent + "• " + body[2..];
            lines[i] = line;
        }
        return string.Join("\r\n", lines).Trim('\r', '\n');
    }

    // ---------- Arguments ----------

    sealed class Args
    {
        static readonly string[] WithValue = ["--title", "--text", "--file", "--image", "--color", "--set"];
        static readonly string[] Switches = ["--all", "--archived", "--clear"];
        readonly Dictionary<string, List<string>> _values = [];
        readonly HashSet<string> _flags = [];

        public string Verb { get; }
        public List<string> Words { get; } = [];

        public Args(IReadOnlyList<string> args)
        {
            Verb = args[0].ToLowerInvariant();
            for (int i = 1; i < args.Count; i++)
            {
                var x = args[i];
                if (WithValue.Contains(x))
                {
                    if (i + 1 >= args.Count)
                        throw new AgentException($"{x} needs a value after it.");
                    if (!_values.TryGetValue(x, out var list))
                        _values[x] = list = [];
                    list.Add(args[++i]);
                }
                else if (Switches.Contains(x))
                    _flags.Add(x);
                else if (x.StartsWith("--"))
                    throw new AgentException($"Unknown option {x}. Run {Command} to see the commands.");
                else
                    Words.Add(x);
            }
        }

        public bool Has(string flag) => _flags.Contains(flag);
        public string? One(string name) => _values.TryGetValue(name, out var list) ? list[^1] : null;
        public List<string> All(string name) => _values.TryGetValue(name, out var list) ? list : [];

        public void NoWords()
        {
            if (Words.Count > 0)
                throw new AgentException($"Unexpected \"{Words[0]}\". Run {Command} to see the commands.");
        }
    }
}
