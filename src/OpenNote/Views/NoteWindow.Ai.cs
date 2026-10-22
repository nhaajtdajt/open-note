using System.IO;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using OpenNote.Core;

namespace OpenNote.Views;

internal partial class NoteWindow
{
    Popup? _aiPopup;
    CancellationTokenSource? _aiCancel;

    // ---------- AI check ----------

    /// <summary>
    /// Sends the selected text (or the current paragraph) to the AI and offers two versions:
    /// one as close as possible to your wording, and one that reads more naturally.
    /// </summary>
    void StartAiCheck()
    {
        if (!App.Instance.Ai.IsConfigured)
        {
            App.Instance.ShowApiKeys(); // first use: show how to add a free key
            return;
        }
        if (Editor.Selection.IsEmpty && CaretParagraph is { } paragraph)
            Editor.Selection.Select(paragraph.ContentStart, paragraph.ContentEnd);
        var original = Editor.Selection.Text.TrimEnd('\r', '\n');
        if (original.Trim().Length == 0)
            return;

        // Pointers that keep marking the selection even if text around it changes meanwhile.
        var start = Editor.Selection.Start.GetPositionAtOffset(0, LogicalDirection.Forward)!;
        var end = Editor.Selection.End.GetPositionAtOffset(0, LogicalDirection.Backward)!;

        if (_aiPopup is not null)
            _aiPopup.IsOpen = false;
        var panel = new AiPanel();
        var popup = new Popup
        {
            Child = panel,
            AllowsTransparency = true,
            StaysOpen = false,
            PlacementTarget = Editor,
            Placement = PlacementMode.Bottom,
            PlacementRectangle = end.GetCharacterRect(LogicalDirection.Backward),
            HorizontalOffset = -10,
        };

        async void Ask()
        {
            _aiCancel?.Cancel();
            var cancel = _aiCancel = new CancellationTokenSource();
            panel.ShowLoading();
            try
            {
                var correction = await App.Instance.Ai.CorrectAsync(original, cancel.Token);
                if (!cancel.IsCancellationRequested)
                    panel.ShowResult(correction, canReplace: !Note.IsLocked);
            }
            catch (AiException ex)
            {
                if (!cancel.IsCancellationRequested)
                    panel.ShowError(ex.Message);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or InvalidOperationException or IOException)
            {
                // Never leave the panel spinning; the details stay out of sight (they could hold a key).
                if (!cancel.IsCancellationRequested)
                    panel.ShowError("Check with AI failed. Please try again.");
            }
        }

        panel.RetryRequested += Ask;
        panel.CloseRequested += () => popup.IsOpen = false;
        panel.Chosen += suggestion =>
        {
            var range = new TextRange(start, end);
            if (Note.IsLocked || range.Text.TrimEnd('\r', '\n') != original)
            {
                // Locked, or the text was edited while waiting: don't overwrite, copy instead.
                TryCopy(suggestion);
                if (!Note.IsLocked)
                {
                    panel.ShowError("The text changed while the AI was answering, so the suggestion was copied to the clipboard instead.");
                    return;
                }
            }
            else
            {
                RichText.RemovePictures(Editor, range.Start, range.End);
                range.Text = suggestion.Replace("\r\n", "\n").Replace("\n", "\r\n");
                Editor.Selection.Select(range.End, range.End);
            }
            popup.IsOpen = false;
            Editor.Focus();
        };
        popup.Closed += (_, _) =>
        {
            _aiCancel?.Cancel();
            if (_aiPopup == popup)
                _aiPopup = null;
        };

        _aiPopup = popup;
        popup.IsOpen = true;
        Ask();
    }

    static void TryCopy(string text)
    {
        try
        {
            Clipboard.SetText(text);
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // Clipboard busy (another app holds it); nothing else to do.
        }
    }
}
