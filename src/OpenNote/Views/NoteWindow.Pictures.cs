using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenNote.Core;

namespace OpenNote.Views;

internal partial class NoteWindow
{
    // ---------- Pictures ----------

    static readonly string[] PictureExtensions = [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tif", ".tiff", ".webp", ".heic"];

    /// <summary>
    /// Pastes or drops pictures: image files copied in Explorer, screenshots (Win+Shift+S) and
    /// images copied from a browser. Clipboard data that also carries text (Word, Excel) pastes
    /// as text, so copying a table never turns into a picture of it.
    /// </summary>
    bool TryInsertPictures(IDataObject data, TextPointer? dropAt)
    {
        var files = (data.GetData(DataFormats.FileDrop) as string[] ?? [])
            .Where(f => PictureExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()) && File.Exists(f))
            .ToList();
        var saved = new List<string>();
        try
        {
            if (files.Count > 0)
            {
                foreach (var file in files)
                    saved.Add(SavePictureFile(file));
            }
            else if (data.GetData(DataFormats.UnicodeText) is string text && text.Trim().Length > 0)
                return false;
            else if (data.GetDataPresent("PNG") && data.GetData("PNG") is MemoryStream png)
                saved.Add(_manager.Images.Save(png.ToArray(), "png")); // keeps transparency, unlike the bitmap format
            else if (data.GetData(DataFormats.Bitmap) is BitmapSource bitmap)
                saved.Add(_manager.Images.Save(EncodePng(bitmap), "png"));
            else
                return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException
                                       or System.Runtime.InteropServices.COMException or FileFormatException)
        {
            if (saved.Count == 0)
            {
                ShowNotice($"Couldn't add the picture: {ex.Message}");
                return true;
            }
        }

        Editor.BeginChange();
        try
        {
            if (dropAt is not null)
                Editor.Selection.Select(dropAt, dropAt);
            foreach (var path in saved)
                InsertPicture(path);
        }
        finally
        {
            Editor.EndChange();
        }
        return true;
    }

    /// <summary>JPEG photos are kept as they are; anything else is stored as PNG.</summary>
    string SavePictureFile(string file)
    {
        var ext = Path.GetExtension(file).ToLowerInvariant();
        if (ext is ".jpg" or ".jpeg")
            return _manager.Images.Save(File.ReadAllBytes(file), "jpg");
        if (ext == ".png")
            return _manager.Images.Save(File.ReadAllBytes(file), "png");
        var decoder = BitmapDecoder.Create(new Uri(file), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        return _manager.Images.Save(EncodePng(decoder.Frames[0]), "png");
    }

    static byte[] EncodePng(BitmapSource bitmap)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Delete, Backspace and Cut on pictures are done by hand (see RichText.RemovePictures);
    /// ordinary text editing is left to the editor.
    /// </summary>
    void Editor_PreviewCommand(object sender, ExecutedRoutedEventArgs e)
    {
        var command = e.Command;
        bool cut = command == ApplicationCommands.Cut;
        bool backward = command == EditingCommands.Backspace || command == EditingCommands.DeletePreviousWord;
        bool forward = command == EditingCommands.Delete || command == EditingCommands.DeleteNextWord;
        if ((!cut && !backward && !forward) || Note.IsLocked)
            return;

        var selection = Editor.Selection;
        if (!selection.IsEmpty)
        {
            if (!RichText.HasPicture(Editor.Document, selection.Start, selection.End))
                return;
            if (cut)
                ApplicationCommands.Copy.Execute(null, Editor); // pictures go to the clipboard as files first
            RichText.RemovePictures(Editor, selection.Start, selection.End);
            if (cut)
            {
                Editor.Selection.Text = "";
                e.Handled = true;
            }
            return;
        }
        if (cut)
            return;

        var next = Editor.CaretPosition.GetAdjacentElement(backward ? LogicalDirection.Backward : LogicalDirection.Forward);
        if (next is InlineUIContainer { Child: PictureView } picture)
        {
            RichText.RemovePictures(Editor, picture.ElementStart, picture.ElementEnd, picture);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Puts a picture on a line of its own at the caret (splitting the line if needed), with
    /// the caret left on the line after it so you can keep typing.
    /// </summary>
    void InsertPicture(string relativePath)
    {
        RichText.RemovePictures(Editor, Editor.Selection.Start, Editor.Selection.End);
        Editor.Selection.Text = "";
        var line = RichText.PictureParagraph(CreatePicture(relativePath));
        var caret = Editor.CaretPosition;
        if (caret.Paragraph is not { } paragraph)
        {
            Editor.Document.Blocks.Add(line);
            var after = new Paragraph();
            Editor.Document.Blocks.Add(after);
            Editor.CaretPosition = after.ContentStart;
            return;
        }

        if (RichText.PlainText(paragraph).Length == 0 || RichText.CharOffset(paragraph, caret) == 0)
        {
            // At the start of a line (or on an empty one): the picture goes above it.
            Editor.Document.Blocks.InsertBefore(paragraph, line);
            return;
        }
        var nextLine = caret.InsertParagraphBreak();
        Editor.Document.Blocks.InsertBefore(nextLine.Paragraph, line);
        Editor.CaretPosition = nextLine;
    }

    /// <summary>
    /// A picture inside the note (see PictureView). It sits in the text like one big character:
    /// click selects it, Delete/Backspace remove it, Ctrl+Z brings it back, double-click opens it.
    /// </summary>
    InlineUIContainer CreatePicture(string relativePath)
    {
        // A picture whose file is missing still keeps its line (PictureView shows "[picture not found]").
        var picture = new PictureView { FitWidth = TextAreaWidth() };
        picture.File = _manager.Images.FullPath(relativePath) ?? "";
        return new InlineUIContainer(picture) { BaselineAlignment = BaselineAlignment.Bottom };
    }

    /// <summary>Usable width for a picture: the editor minus its padding, the thin scrollbar and the caret.</summary>
    double TextAreaWidth() =>
        Editor.ActualWidth > 0
            ? Math.Max(40, Editor.ActualWidth - Editor.Padding.Left - Editor.Padding.Right - 10)
            : double.PositiveInfinity;

    void UpdatePictureWidths()
    {
        double width = TextAreaWidth();
        foreach (var container in RichText.Pictures(Editor.Document))
        {
            if (container.Child is PictureView picture)
                picture.FitWidth = width;
        }
    }

    /// <summary>The picture under the mouse, if any (pictures are inert content, so look them up by position).</summary>
    InlineUIContainer? PictureUnderMouse(MouseEventArgs e)
    {
        var point = e.GetPosition(Editor);
        foreach (var container in RichText.Pictures(Editor.Document))
        {
            if (container.Child is not FrameworkElement { IsVisible: true } element)
                continue;
            try
            {
                var bounds = element.TransformToAncestor(Editor).TransformBounds(new Rect(element.RenderSize));
                if (bounds.Contains(point))
                    return container;
            }
            catch (InvalidOperationException)
            {
                // Not laid out yet.
            }
        }
        return null;
    }

    /// <summary>Highlights pictures that are part of the selection (the text highlight doesn't cover them well).</summary>
    void UpdatePictureSelection()
    {
        var selection = Editor.Selection;
        foreach (var container in RichText.Pictures(Editor.Document))
        {
            if (container.Child is not PictureView picture)
                continue;
            picture.IsSelected = !selection.IsEmpty
                && selection.Start.CompareTo(container.ElementEnd) < 0 && selection.End.CompareTo(container.ElementStart) > 0;
        }
    }

    /// <summary>
    /// Copying pictures puts the original files (and, for one picture, the image itself) on the
    /// clipboard, so they paste at full quality into another note, a chat app or Explorer.
    /// </summary>
    void Editor_Copying(object sender, DataObjectCopyingEventArgs e)
    {
        var selection = Editor.Selection;
        var files = RichText.Pictures(Editor.Document)
            .Where(b => selection.Start.CompareTo(b.ElementEnd) < 0 && selection.End.CompareTo(b.ElementStart) > 0)
            .Select(b => ((PictureView)b.Child).File)
            .Where(File.Exists)
            .ToArray();
        if (files.Length == 0)
            return;
        e.DataObject.SetData(DataFormats.FileDrop, files);
        if (files.Length == 1 && selection.Text.Trim().Length == 0)
        {
            try
            {
                e.DataObject.SetData(DataFormats.Bitmap, BitmapFrame.Create(new Uri(files[0]), BitmapCreateOptions.None, BitmapCacheOption.OnLoad));
            }
            catch (Exception ex) when (ex is IOException or NotSupportedException or FileFormatException)
            {
                // The file copy alone still works.
            }
        }
    }

    static void OpenPicture(string file)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(file) { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // No app registered for this picture type.
        }
    }

    void Editor_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (!Note.IsLocked && (e.Data.GetData(DataFormats.FileDrop) as string[] ?? [])
                .Any(f => PictureExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    void Editor_PreviewDrop(object sender, DragEventArgs e)
    {
        if (Note.IsLocked || !e.Data.GetDataPresent(DataFormats.FileDrop))
            return;
        var at = Editor.GetPositionFromPoint(e.GetPosition(Editor), snapToText: true) ?? Editor.CaretPosition;
        if (TryInsertPictures(e.Data, dropAt: at))
        {
            Activate();
            Editor.Focus();
            e.Handled = true;
        }
    }

    /// <summary>A short message under the header that fades away on its own.</summary>
    void ShowNotice(string message)
    {
        var popup = new Popup
        {
            PlacementTarget = Editor,
            Placement = PlacementMode.Top,
            AllowsTransparency = true,
            StaysOpen = false,
            Child = new Border
            {
                Background = Brush(0xEE, (0x2B, 0x2B, 0x2B)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 6, 10, 6),
                MaxWidth = 300,
                Child = new TextBlock { Text = message, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap },
            },
        };
        popup.IsOpen = true;
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            popup.IsOpen = false;
        };
        timer.Start();
    }
}
