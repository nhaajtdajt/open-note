using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenNote.Core;

/// <summary>
/// A note saved as a file: plain text (.txt), or an .onote file that keeps everything (the
/// formatting, the paper color and glass, the font, the mark and the pictures) so it opens again
/// in Open Note as the same note, on this PC or another. Saving makes a copy; the note stays.
/// </summary>
public static class NoteFile
{
    public const string Extension = ".onote";
    const string FormatName = "Open Note";
    const int FormatVersion = 1;

    /// <summary>Bigger files are refused before reading (a note with ten large photos is far smaller).</summary>
    public const long MaxBytes = 200L * 1024 * 1024;

    /// <summary>The note as plain text: the title, a blank line, then the text without formatting marks (a picture becomes "[image]").</summary>
    public static string ToText(Note note)
    {
        var lines = note.Text.Replace("\r\n", "\n").Split('\n')
            .Select(line => ImageStore.IsImageLine(line, out _) ? Ui.T("[image]") : InlineMarkup.Strip(line));
        var body = string.Join("\r\n", lines).Trim('\r', '\n');
        var title = note.Heading.Trim(); // titles are plain text: * and { are kept as typed
        return title.Length == 0 ? body + "\r\n" : title + "\r\n\r\n" + body + "\r\n";
    }

    /// <summary>The bytes of a .txt file (UTF-8, which Notepad and every editor read).</summary>
    public static byte[] ToTextFile(Note note) => new UTF8Encoding(false).GetBytes(ToText(note));

    /// <summary>The bytes of an .onote file; <paramref name="readPicture"/> gives a picture's bytes (null if it's gone).</summary>
    public static byte[] ToOnote(Note note, Func<string, byte[]?> readPicture)
    {
        var file = new OnoteContents
        {
            Format = FormatName,
            Version = FormatVersion,
            Heading = note.Heading,
            Text = note.Text,
            Color = note.Color,
            Opacity = note.Opacity,
            FontSize = note.FontSize,
            Font = note.Font,
            SpellCheck = note.SpellCheck,
            IsImportant = note.IsImportant,
            Mark = note.Mark,
            Width = note.Width,
            Height = note.Height,
        };
        foreach (var path in ImageStore.Referenced(note.Text).Distinct())
        {
            if (readPicture(path) is { } bytes)
                file.Pictures[path] = Convert.ToBase64String(bytes);
        }
        return JsonSerializer.SerializeToUtf8Bytes(file, NoteStore.JsonOptions);
    }

    /// <summary>
    /// Reads an .onote file into a new note (not yet added anywhere); its pictures are stored with
    /// <paramref name="savePicture"/> (bytes, "png"/"jpg" → the new path) and the text points at them.
    /// Throws <see cref="InvalidDataException"/> with a message for the user when it can't be read.
    /// </summary>
    public static Note FromOnote(byte[] bytes, Func<byte[], string, string> savePicture)
    {
        OnoteContents? file;
        try
        {
            file = JsonSerializer.Deserialize<OnoteContents>(NoteStore.WithoutBom(bytes), NoteStore.JsonOptions);
        }
        catch (JsonException)
        {
            file = null;
        }
        if (file is null || file.Format != FormatName)
            throw new InvalidDataException(Ui.T("This isn't an Open Note file."));
        if (file.Version > FormatVersion)
            throw new InvalidDataException(Ui.T("This note was saved by a newer Open Note. Update Open Note to open it."));

        var text = file.Text ?? "";
        foreach (var (path, data) in file.Pictures ?? [])
        {
            // Only pictures the note's own picture lines refer to, and only real PNG or JPEG data.
            if (!text.Contains(ImageStore.Line(path), StringComparison.Ordinal) || !ImageStore.IsImageLine(ImageStore.Line(path), out _))
                continue;
            byte[] picture;
            try
            {
                picture = Convert.FromBase64String(data ?? "");
            }
            catch (FormatException)
            {
                continue;
            }
            if (PictureType(picture) is not { } type)
                continue;
            text = text.Replace(ImageStore.Line(path), ImageStore.Line(savePicture(picture, type)), StringComparison.Ordinal);
        }

        var (r, g, b) = Palette.ParseRgb(file.Color);
        return new Note
        {
            Heading = file.Heading ?? "",
            Text = text,
            Color = $"#{r:X2}{g:X2}{b:X2}",
            Opacity = double.IsFinite(file.Opacity) ? Math.Clamp(file.Opacity, 0.2, 1) : 0.9,
            FontSize = double.IsFinite(file.FontSize) ? Math.Clamp(file.FontSize, 9, 40) : 14,
            Font = Enum.IsDefined(file.Font) ? file.Font : NoteFont.Mono,
            SpellCheck = file.SpellCheck,
            IsImportant = file.IsImportant,
            Mark = NoteMarks.IsFlower(file.Mark) ? file.Mark! : NoteMarks.Star,
            Width = Math.Clamp(file.Width, 0, 8000),
            Height = Math.Clamp(file.Height, 0, 8000),
        };
    }

    /// <summary>
    /// Writes the file next to its final place first, then swaps it in, so a full disk can't
    /// destroy an earlier copy being replaced.
    /// </summary>
    public static void WriteFile(string path, byte[] bytes)
    {
        var temp = path + ".tmp";
        try
        {
            using (var fs = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.Write(bytes);
                fs.Flush(flushToDisk: true);
            }
            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            try
            {
                File.Delete(temp);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
            throw;
        }
    }

    /// <summary>"png" or "jpg" from the first bytes, else null.</summary>
    static string? PictureType(byte[] b) =>
        b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 ? "png"
        : b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF ? "jpg"
        : null;

    /// <summary>A file name for the note: its name without characters Windows doesn't allow.</summary>
    public static string SuggestedName(Note note)
    {
        var name = string.Concat(note.Title.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? ' ' : c)).Trim().TrimEnd('.');
        if (name.Length > 60)
            name = name[..60].TrimEnd();
        return name.Length == 0 ? "Note" : name;
    }

    /// <summary>What an .onote file holds (camelCase JSON, like notes.json).</summary>
    sealed class OnoteContents
    {
        public string Format { get; set; } = "";
        public int Version { get; set; }
        public string? Heading { get; set; }
        public string? Text { get; set; }
        public string? Color { get; set; }
        public double Opacity { get; set; } = 0.9;
        public double FontSize { get; set; } = 14;
        public NoteFont Font { get; set; } = NoteFont.Mono;
        public bool? SpellCheck { get; set; }
        public bool IsImportant { get; set; }
        public string? Mark { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        /// <summary>Each picture the text refers to ("images/….png"), as base64.</summary>
        public Dictionary<string, string?>? Pictures { get; set; } = [];
    }
}
