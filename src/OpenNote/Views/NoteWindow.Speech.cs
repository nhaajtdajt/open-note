using System.Text.RegularExpressions;
using System.Windows.Documents;
using OpenNote.Core;
using OpenNote.Services;

namespace OpenNote.Views;

internal partial class NoteWindow
{
    /// <summary>
    /// Reads the selected text aloud (or the current line if nothing is selected) with the offline
    /// Windows voices; pressing it again while reading stops.
    /// </summary>
    void ReadAloud()
    {
        var speech = App.Instance.Speech;
        if (speech.IsSpeaking)
        {
            speech.Stop();
            return;
        }

        var range = Editor.Selection.IsEmpty && CaretParagraph is { } paragraph
            ? new TextRange(paragraph.ContentStart, paragraph.ContentEnd)
            : Editor.Selection;
        var text = SpeakableText(range.Text);
        if (text.Length == 0)
            return;

        var s = _manager.Settings;
        var problem = speech.Speak(text, LanguageHint.Guess(text, s.Native(), s.Learning()));
        if (problem is not null)
            ShowNotice(problem);
    }

    /// <summary>The text without list markers and picture placeholders, so only words are read.</summary>
    static string SpeakableText(string text)
    {
        text = text.Replace(RichText.PictureChar.ToString(), "");
        text = Regex.Replace(text, @"(?m)^\s*[☐☑•]\s*", "");
        return Regex.Replace(text, @"\s+", " ").Trim();
    }
}
