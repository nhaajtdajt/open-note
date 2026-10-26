namespace OpenNote.Core;

/// <summary>
/// The app's words in the display language (picked at the first start, changed on the
/// Ctrl+Alt+/ card). The English text is the key: <c>Ui.T("New note")</c>. A phrase missing
/// from a language stays in English rather than disappearing.
/// </summary>
public static class Ui
{
    public static Language Current { get; private set; } = Language.English;

    public static void Use(Language language) => Current = language;

    public static string T(string english) =>
        Current.Code != "en" && Translations.Table.TryGetValue(Current.Code, out var table) && table.TryGetValue(english, out var text)
            ? text
            : english;

    /// <summary>A phrase with {0}-style blanks, filled after translating.</summary>
    public static string T(string english, params object?[] args) => string.Format(T(english), args);
}
