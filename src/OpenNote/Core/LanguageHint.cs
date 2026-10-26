namespace OpenNote.Core;

public static class LanguageHint
{
    const string SharedAccents = "àáâãèéêìíòóôõùúýÀÁÂÃÈÉÊÌÍÒÓÔÕÙÚÝ";

    /// <summary>
    /// True when the text is written in Vietnamese. Windows' spell checker only knows English
    /// here, so on Vietnamese text it would underline nearly every word.
    /// One letter only Vietnamese uses (ạ, ế, đ, ơ, ư, ă, ...) is enough; accents Vietnamese
    /// shares with French or Spanish (à, é, ô, ...) need two, so "café" alone doesn't count.
    /// </summary>
    public static bool LooksVietnamese(string text)
    {
        int shared = 0;
        foreach (char c in text)
        {
            if (IsVietnameseOnly(c))
                return true;
            if (SharedAccents.Contains(c) && ++shared >= 2)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Which of your two languages a piece of text is in, for read aloud and spell check.
    /// Vietnamese, Chinese, Japanese and Korean are recognised from their letters; English,
    /// Spanish and French can't be told apart that way, so such text counts as the language
    /// you're learning.
    /// </summary>
    public static Language Guess(string text, Language native, Language learning)
    {
        if (native == learning)
            return learning;
        if (Recognised(learning, text))
            return learning;
        if (Recognised(native, text))
            return native;
        // Known not to be a recognisable language you're learning: then it's yours.
        return CanRecognise(learning) && !CanRecognise(native) ? native : learning;
    }

    static bool CanRecognise(Language l) => l.Code is "vi" or "zh" or "ja" or "ko";

    static bool Recognised(Language l, string text) => l.Code switch
    {
        "vi" => LooksVietnamese(text),
        "ja" => text.Any(IsKana),
        "zh" => text.Any(IsHan) && !text.Any(IsKana),
        "ko" => text.Any(IsHangul),
        _ => false,
    };

    static bool IsKana(char c) => c is >= '぀' and <= 'ヿ';
    static bool IsHan(char c) => c is >= '一' and <= '鿿' or >= '㐀' and <= '䶿';
    static bool IsHangul(char c) => c is >= '가' and <= '힣' or >= 'ᄀ' and <= 'ᇿ' or >= '㄰' and <= '㆏';

    static bool IsVietnameseOnly(char c) =>
        c is >= 'Ạ' and <= 'ỹ'                // Latin Extended Additional: ạ ả ấ ầ ... ỹ
        or 'đ' or 'Đ' or 'ơ' or 'Ơ' or 'ư' or 'Ư' or 'ă' or 'Ă'
        or 'ĩ' or 'Ĩ' or 'ũ' or 'Ũ';
}
