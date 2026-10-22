using System.IO;
using System.Text.RegularExpressions;

namespace OpenNote.Core;

/// <summary>
/// Pictures pasted into notes. Each is a file in the data folder's "images" subfolder, and the
/// note keeps a line like "![image](images/3f2a….png)" where the picture sits, so notes.json
/// stays small and plain.
/// </summary>
public sealed partial class ImageStore(string dataDirectory)
{
    public const string FolderName = "images";

    // Only names this app generates are accepted, so a hand-edited notes.json can't point outside the folder.
    [GeneratedRegex(@"^images/[0-9a-f]{32}\.(png|jpg)$")]
    private static partial Regex SafePath();

    [GeneratedRegex(@"^!\[image\]\((?<path>[^)\r\n]+)\)$")]
    private static partial Regex ImageLinePattern();

    public string Folder => Path.Combine(dataDirectory, FolderName);

    /// <summary>The line stored in a note for a picture.</summary>
    public static string Line(string relativePath) => $"![image]({relativePath})";

    /// <summary>True when the whole line is a picture written by <see cref="Line"/>.</summary>
    public static bool IsImageLine(string line, out string relativePath)
    {
        var match = ImageLinePattern().Match(line.Trim());
        relativePath = match.Success ? match.Groups["path"].Value : "";
        return match.Success && SafePath().IsMatch(relativePath);
    }

    /// <summary>All pictures a note's text refers to.</summary>
    public static IEnumerable<string> Referenced(string text)
    {
        foreach (var line in text.Split('\n'))
        {
            if (IsImageLine(line, out var path))
                yield return path;
        }
    }

    /// <summary>Absolute path of a stored picture, or null for anything that isn't one of ours.</summary>
    public string? FullPath(string relativePath) =>
        SafePath().IsMatch(relativePath) ? Path.Combine(dataDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar)) : null;

    /// <summary>Saves encoded picture bytes ("png" or "jpg") and returns the path to store in the note.</summary>
    public string Save(byte[] encoded, string extension)
    {
        if (extension is not ("png" or "jpg"))
            throw new ArgumentException("Only png and jpg are stored.", nameof(extension));
        Directory.CreateDirectory(Folder);
        var relative = $"{FolderName}/{Guid.NewGuid():N}.{extension}";
        File.WriteAllBytes(FullPath(relative)!, encoded);
        return relative;
    }

    /// <summary>
    /// Deletes pictures no note refers to any more (removed from a note, or the note was deleted).
    /// Run at startup only, after the notes loaded cleanly, never while notes are being edited.
    /// </summary>
    public int DeleteUnreferenced(IEnumerable<string> noteTexts)
    {
        if (!Directory.Exists(Folder))
            return 0;
        var used = new HashSet<string>(noteTexts.SelectMany(Referenced).Select(p => Path.GetFileName(p)), StringComparer.OrdinalIgnoreCase);
        int deleted = 0;
        foreach (var file in Directory.EnumerateFiles(Folder))
        {
            if (used.Contains(Path.GetFileName(file)))
                continue;
            try
            {
                File.Delete(file);
                deleted++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // In use (e.g. open in a viewer) or read-only; try again next start.
            }
        }
        return deleted;
    }
}
