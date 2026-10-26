namespace OpenNote.Core;

/// <summary>
/// A language for Check with AI, spell check and read aloud: your own language, or the one
/// you're learning (both chosen in the keys window, Ctrl+Alt+K).
/// </summary>
/// <param name="EnglishName">How the AI is told about it.</param>
/// <param name="NativeName">How it is shown in the language pickers.</param>
/// <param name="Culture">Spell-check language (the Windows dictionary used, if installed).</param>
/// <param name="VoiceId">Primary language id of the Windows voices that read it aloud.</param>
public sealed record Language(string Code, string EnglishName, string NativeName, string Culture, int VoiceId)
{
    public static readonly IReadOnlyList<Language> All =
    [
        new("en", "English", "English", "en-US", 0x09),
        new("vi", "Vietnamese", "Tiếng Việt", "vi-VN", 0x2A),
        new("zh", "Simplified Chinese", "中文（简体）", "zh-CN", 0x04),
        new("es", "Spanish", "Español", "es-ES", 0x0A),
        new("ja", "Japanese", "日本語", "ja-JP", 0x11),
        new("ko", "Korean", "한국어", "ko-KR", 0x12),
        new("fr", "French", "Français", "fr-FR", 0x0C),
    ];

    public static Language English => All[0];

    /// <summary>The language with this code, or null.</summary>
    public static Language? Find(string? code) => All.FirstOrDefault(l => l.Code == code);

    /// <summary>
    /// Your language when you haven't picked one: Windows' display language if it is one of
    /// these and not English; otherwise the region's format language (many people run Windows in
    /// English with their own region); otherwise English.
    /// </summary>
    public static Language Guess(string displayLanguage, string formatLanguage) =>
        Find(displayLanguage) is { } display && display != English ? display : Find(formatLanguage) ?? English;

    public override string ToString() => NativeName; // what the pickers show
}
