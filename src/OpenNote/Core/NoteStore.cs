using System.IO;
using System.Diagnostics.CodeAnalysis;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenNote.Core;

/// <summary>
/// Reads and writes notes.json. Writes go to a temp file that then replaces the real file
/// (keeping the previous version as notes.json.bak), so a crash mid-save can't leave a
/// half-written file behind.
/// </summary>
public sealed class NoteStore(string directory)
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // Keep non-ASCII (e.g. Vietnamese) readable in the file instead of \uXXXX escapes.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    public string Directory { get; } = directory;
    public string FilePath => Path.Combine(Directory, "notes.json");
    public string BackupPath => FilePath + ".bak";
    string TempPath => FilePath + ".tmp";

    /// <summary>True when no notes file existed (used to show the welcome note once).</summary>
    public bool IsFirstRun { get; private set; }

    /// <summary>
    /// True when the notes came from notes.json itself (or there were none yet). After a fallback
    /// to the backup, files that only the newest notes used must not be cleaned up.
    /// </summary>
    public bool LoadedCleanly { get; private set; }

    /// <summary>
    /// Loads the notes. A damaged file is set aside and the backup used instead; a file that is
    /// only locked (antivirus, sync tools) is retried, and still failing throws an IOException
    /// rather than being mistaken for damage.
    /// </summary>
    public StoreData Load()
    {
        System.IO.Directory.CreateDirectory(Directory);
        if (!File.Exists(FilePath) && !File.Exists(BackupPath))
        {
            IsFirstRun = LoadedCleanly = true;
            return new StoreData();
        }

        if (TryRead(FilePath, out var data))
        {
            LoadedCleanly = true;
            return data;
        }

        // The main file is missing or damaged (or from a newer version). Set it aside, never
        // silently discard anything, then fall back to the previous good version.
        SetAside(FilePath, "json");
        if (TryRead(BackupPath, out data))
            return data;
        SetAside(BackupPath, "bak.json"); // otherwise the second save would overwrite it
        return new StoreData();
    }

    void SetAside(string path, string extension)
    {
        if (File.Exists(path))
            File.Move(path, Path.Combine(Directory, $"notes.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}.{extension}"), overwrite: true);
    }

    public void Save(StoreData data)
    {
        System.IO.Directory.CreateDirectory(Directory);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(data, JsonOptions);
        using (var fs = new FileStream(TempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            fs.Write(bytes);
            fs.Flush(flushToDisk: true);
        }

        if (File.Exists(FilePath))
            File.Replace(TempPath, FilePath, BackupPath, ignoreMetadataErrors: true);
        else
            File.Move(TempPath, FilePath);
    }

    static bool TryRead(string path, [NotNullWhen(true)] out StoreData? data)
    {
        data = null;
        if (!File.Exists(path))
            return false;
        var bytes = ReadWithRetry(path);
        try
        {
            data = JsonSerializer.Deserialize<StoreData>(WithoutBom(bytes), JsonOptions);
            if (data is null)
                return false;
            data.Notes = (data.Notes ?? []).Where(n => n is not null).ToList();
            data.FocusSet ??= [];
            foreach (var n in data.Notes)
            {
                n.Text ??= "";
                n.Heading ??= "";
                if (n.Handwriting == true)
                    n.Font = NoteFont.Hand; // notes from before fonts were selectable
                n.Handwriting = null;
            }
            return true;
        }
        catch (JsonException)
        {
            data = null;
            return false;
        }
    }

    /// <summary>
    /// The JSON without a UTF-8 byte order mark, which the parser rejects but editors such as
    /// Notepad on older Windows 10 write.
    /// </summary>
    internal static ReadOnlySpan<byte> WithoutBom(byte[] bytes) =>
        bytes.AsSpan().StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]) ? bytes.AsSpan(3) : bytes;

    static byte[] ReadWithRetry(string path)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                return File.ReadAllBytes(path);
            }
            catch (IOException) when (attempt < 10)
            {
                Thread.Sleep(300);
            }
        }
    }
}
