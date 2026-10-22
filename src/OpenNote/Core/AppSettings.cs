using System.IO;
using System.Text.Json;

namespace OpenNote.Core;

/// <summary>
/// User-editable settings in settings.json. The app writes it only to create it and through
/// <see cref="Update"/>, which keeps hand edits.
/// </summary>
public sealed class AppSettings
{
    // Global shortcuts. Empty string disables one.
    public string NewNoteHotkey { get; set; } = "Ctrl+Alt+N";

    /// <summary>Show/hide: brings notes to the front, or hides them if you're already in a note.</summary>
    public string ToggleAllHotkey { get; set; } = "Ctrl+Alt+H";

    public string NotesListHotkey { get; set; } = "Ctrl+Alt+M";

    /// <summary>Show all notes, then pick the ones to keep on screen (the choice is saved).</summary>
    public string PickNotesHotkey { get; set; } = "Ctrl+Alt+P";

    /// <summary>Show only the saved selection; again to hide everything.</summary>
    public string SavedNotesHotkey { get; set; } = "Ctrl+Alt+J";

    /// <summary>Opens the window for adding Gemini API keys.</summary>
    public string ApiKeysHotkey { get; set; } = "Ctrl+Alt+K";

    /// <summary>
    /// Set once the app has turned on "Start with Windows" by itself on its first run, so it never
    /// switches it back on after you turn it off.
    /// </summary>
    public bool AutoStartConfigured { get; set; }

    /// <summary>Keep every note above other windows, so clicking into another app doesn't cover them.</summary>
    public bool KeepNotesOnTop { get; set; } = true;

    public string DefaultColor { get; set; } = Palette.Default.Hex;
    public double DefaultOpacity { get; set; } = 0.9;
    public NoteFont DefaultFont { get; set; } = NoteFont.Mono;
    public double DefaultFontSize { get; set; } = 14;

    /// <summary>
    /// OpenCode Zen API key (free models such as Big Pickle; create one at opencode.ai).
    /// If empty, the OPENCODE_API_KEY environment variable is used. When set, OpenCode is tried
    /// first and Gemini is the fallback.
    /// </summary>
    public string OpenCodeApiKey { get; set; } = "";

    /// <summary>OpenCode models to try in order (all free).</summary>
    public List<string> OpenCodeModels { get; set; } = ["big-pickle", "deepseek-v4.1-flash-free", "glm-5.3-flash-free"];

    /// <summary>
    /// Any other OpenAI-compatible service (OpenRouter, Groq, Cerebras, Mistral...), tried after
    /// OpenCode and before Gemini. Example:
    /// { "name": "OpenRouter", "baseUrl": "https://openrouter.ai/api/v1", "apiKey": "...", "models": ["openrouter/free"] }
    /// </summary>
    public List<AiServiceSetting> AiServices { get; set; } = [];

    /// <summary>Gemini API keys, used in turn. If empty, GEMINI_API_KEY, GEMINI_API_KEY_2, ... environment variables are used.</summary>
    public List<string> GeminiApiKeys { get; set; } = [];

    /// <summary>Gemini models to try in order; the next one is used when one is overloaded.</summary>
    public List<string> GeminiModels { get; set; } =
        ["gemini-flash-latest", "gemini-flash-lite-latest", "gemini-2.5-flash", "gemini-2.5-flash-lite"];

    public IReadOnlyList<string> EffectiveGeminiKeys()
    {
        if (GeminiApiKeys.Count > 0)
            return GeminiApiKeys;
        var fromEnv = new List<string>();
        for (int i = 1; i <= 20; i++)
        {
            if (Environment.GetEnvironmentVariable(i == 1 ? "GEMINI_API_KEY" : $"GEMINI_API_KEY_{i}") is { Length: > 0 } key)
                fromEnv.Add(key);
        }
        return fromEnv;
    }

    public IReadOnlyList<string> EffectiveOpenCodeKeys()
    {
        var key = !string.IsNullOrWhiteSpace(OpenCodeApiKey) ? OpenCodeApiKey : Environment.GetEnvironmentVariable("OPENCODE_API_KEY");
        return string.IsNullOrWhiteSpace(key) ? [] : [key];
    }

    /// <summary>The AI services to use, in the order they are tried (providers without a key are skipped).</summary>
    public AiCorrector CreateAiCorrector(System.Net.Http.HttpClient? client = null) => new(
    [
        new AiProvider("OpenCode", AiProvider.ApiStyle.OpenAiChat, AiProvider.OpenCodeUrl, EffectiveOpenCodeKeys(), OpenCodeModels),
        .. (AiServices ?? []).Where(x => !string.IsNullOrWhiteSpace(x.BaseUrl))
            .Select(x => new AiProvider(string.IsNullOrWhiteSpace(x.Name) ? x.BaseUrl : x.Name, AiProvider.ApiStyle.OpenAiChat, x.BaseUrl,
                string.IsNullOrWhiteSpace(x.ApiKey) ? [] : [x.ApiKey], x.Models ?? [])),
        new AiProvider("Gemini", AiProvider.ApiStyle.Gemini, AiProvider.GeminiUrl, EffectiveGeminiKeys(), GeminiModels),
    ], client);

    public static string PathIn(string directory) => Path.Combine(directory, "settings.json");

    // Hand edits are forgiven a trailing comma or a // comment.
    static readonly JsonSerializerOptions JsonOptions = new(NoteStore.JsonOptions)
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>
    /// Changes a setting here and in settings.json (used by the tray menu and the keys window).
    /// The file is read again first so hand edits since the last load are kept, and it is never
    /// written while it can't be read. Returns an error message, or null when saved.
    /// </summary>
    public string? Update(string directory, Action<AppSettings> change)
    {
        change(this);
        var path = PathIn(directory);
        try
        {
            var onDisk = File.Exists(path) ? Read(path) : new AppSettings();
            change(onDisk);
            Write(path, onDisk);
            return null;
        }
        catch (JsonException)
        {
            return "settings.json has a mistake in it, so it wasn't changed. Fix it (tray icon → Edit settings), then Reload settings.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"settings.json couldn't be saved: {ex.Message}";
        }
    }

    /// <summary>Loads settings, creating the file with defaults if missing. Returns an error message for an unreadable file.</summary>
    public static AppSettings Load(string directory, out string? error)
    {
        error = null;
        var path = PathIn(directory);
        try
        {
            if (File.Exists(path))
                return Read(path);

            var defaults = new AppSettings();
            System.IO.Directory.CreateDirectory(directory);
            Write(path, defaults);
            return defaults;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            error = $"settings.json could not be read ({ex.Message}). Using defaults until it's fixed.";
            return new AppSettings { ReadFailed = true };
        }
    }

    /// <summary>True when settings.json exists but couldn't be read: these are stand-in defaults.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool ReadFailed { get; private init; }

    static AppSettings Read(string path)
    {
        var loaded = JsonSerializer.Deserialize<AppSettings>(NoteStore.WithoutBom(File.ReadAllBytes(path)), JsonOptions) ?? new AppSettings();
        loaded.OpenCodeApiKey ??= "";
        if (loaded.OpenCodeModels is null or { Count: 0 })
            loaded.OpenCodeModels = new AppSettings().OpenCodeModels;
        loaded.AiServices ??= [];
        loaded.GeminiApiKeys ??= [];
        if (loaded.GeminiModels is null or { Count: 0 })
            loaded.GeminiModels = new AppSettings().GeminiModels;
        return loaded;
    }

    /// <summary>Writes to a temp file first, so a crash mid-write can't leave half a file (and lose the keys).</summary>
    static void Write(string path, AppSettings settings)
    {
        var temp = path + ".tmp";
        File.WriteAllBytes(temp, JsonSerializer.SerializeToUtf8Bytes(settings, JsonOptions));
        File.Move(temp, path, overwrite: true);
    }
}

/// <summary>One extra OpenAI-compatible AI service in settings.json.</summary>
public sealed class AiServiceSetting
{
    public string Name { get; set; } = "";
    public string BaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public List<string> Models { get; set; } = [];
}
