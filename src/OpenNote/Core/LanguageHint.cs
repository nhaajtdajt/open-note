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

    static bool IsVietnameseOnly(char c) =>
        c is >= 'Ạ' and <= 'ỹ'                // Latin Extended Additional: ạ ả ấ ầ ... ỹ
        or 'đ' or 'Đ' or 'ơ' or 'Ơ' or 'ư' or 'Ư' or 'ă' or 'Ă'
        or 'ĩ' or 'Ĩ' or 'ũ' or 'Ũ';
}
