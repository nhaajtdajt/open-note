using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenNote.Core;
using OpenNote.Views;

namespace OpenNote.Tests;

public sealed class PictureTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "OpenNotePics-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    static T OnSta<T>(Func<T> work)
    {
        T result = default!;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { result = work(); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null)
            throw new AggregateException(error);
        return result;
    }

    /// <summary>A real PNG of the given size, saved through the store like a paste would.</summary>
    string SavePng(ImageStore store, int width, int height) => OnSta(() =>
    {
        var pixels = new byte[width * height * 4];
        for (int i = 0; i < pixels.Length; i += 4)
            (pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]) = (40, 120, 220, 255);
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return store.Save(stream.ToArray(), "png");
    });

    [Fact]
    public void ImageLines_RoundTrip_AndOnlyOurOwnPathsAreAccepted()
    {
        Assert.True(ImageStore.IsImageLine(ImageStore.Line("images/0123456789abcdef0123456789abcdef.png"), out var path));
        Assert.Equal("images/0123456789abcdef0123456789abcdef.png", path);
        Assert.False(ImageStore.IsImageLine("![image](../../Windows/system32/x.png)", out _));
        Assert.False(ImageStore.IsImageLine("![image](C:/secret.png)", out _));
        Assert.False(ImageStore.IsImageLine("see ![image](images/0123456789abcdef0123456789abcdef.png) inline", out _));
        Assert.Null(new ImageStore(_dir).FullPath("../notes.json"));
    }

    [Fact]
    public void NoteWithPicture_ShowsImagePlaceholderInLists()
    {
        var note = new Note { Text = "Bug screenshot\r\n" + ImageStore.Line("images/0123456789abcdef0123456789abcdef.png") };
        Assert.Equal("Bug screenshot", note.Title);
        Assert.Equal("[image]", note.Preview);
    }

    [Fact]
    public void UnreferencedPictures_AreCleanedUp_ReferencedOnesKept()
    {
        var store = new ImageStore(_dir);
        var kept = SavePng(store, 4, 4);
        var dropped = SavePng(store, 4, 4);
        int deleted = store.DeleteUnreferenced(["hello\r\n" + ImageStore.Line(kept)]);
        Assert.Equal(1, deleted);
        Assert.True(File.Exists(store.FullPath(kept)));
        Assert.False(File.Exists(store.FullPath(dropped)));
    }

    static InlineUIContainer Picture(ImageStore store, string path) =>
        new(new PictureView { File = store.FullPath(path)! });

    [Fact]
    public void Document_WithPicture_SavesBackTheSameText()
    {
        var store = new ImageStore(_dir);
        var path = SavePng(store, 8, 8);
        var text = "before\r\n" + ImageStore.Line(path) + "\r\nafter";
        Assert.Equal(text, OnSta(() =>
        {
            var doc = new FlowDocument();
            RichText.Load(doc, text, p => Picture(store, p));
            Assert.Single(RichText.Pictures(doc));
            return RichText.Save(doc);
        }));
    }

    /// <summary>The reported bug: after reopening a note that starts with a picture you couldn't type or delete.</summary>
    [Fact]
    public void ReopenedNote_StartingWithPicture_CanBeTypedIn_AndPictureDeleted()
    {
        var store = new ImageStore(_dir);
        var path = SavePng(store, 8, 8);
        var text = ImageStore.Line(path) + "\r\nhello\r\n" + ImageStore.Line(path);
        var (typed, deletedOne, deletedAll) = OnSta(() =>
        {
            var box = new RichTextBox();
            var host = new Window { Content = box, Width = 300, Height = 200, ShowActivated = false, ShowInTaskbar = false, Left = -4000 };
            host.Show();
            RichText.Load(box.Document, text, p => Picture(store, p));

            // Typing next to the picture works.
            box.CaretPosition = RichText.PositionAt((Paragraph)box.Document.Blocks.ElementAt(1), 5);
            box.CaretPosition.InsertTextInRun("!");
            var typedText = RichText.Save(box.Document);

            // Deleting one picture (as the Delete key does) works and keeps the rest.
            var pictures = RichText.Pictures(box.Document).ToList();
            RichText.RemovePictures(box, pictures[0].ElementStart, pictures[0].ElementEnd, pictures[0]);
            var one = RichText.Save(box.Document);

            // Select everything and delete: pictures first, then the text; typing still works afterwards.
            RichText.RemovePictures(box, box.Document.ContentStart, box.Document.ContentEnd);
            box.SelectAll();
            box.Selection.Text = "";
            box.CaretPosition.InsertTextInRun("new");
            var all = RichText.Save(box.Document);
            host.Close();
            return (typedText, one, all);
        });

        Assert.Equal(ImageStore.Line(path) + "\r\nhello!\r\n" + ImageStore.Line(path), typed);
        Assert.Equal("hello!\r\n" + ImageStore.Line(path), deletedOne);
        Assert.Equal("new", deletedAll);
    }

    [Fact]
    public void TextTypedNextToAPicture_IsSavedOnItsOwnLine()
    {
        var store = new ImageStore(_dir);
        var path = SavePng(store, 8, 8);
        Assert.Equal("abc\r\n" + ImageStore.Line(path) + "\r\ndef", OnSta(() =>
        {
            var doc = new FlowDocument();
            var p = new Paragraph();
            p.Inlines.Add(new Run("abc"));
            p.Inlines.Add(Picture(store, path));
            p.Inlines.Add(new Run("def"));
            doc.Blocks.Add(p);
            return RichText.Save(doc);
        }));
    }

    /// <summary>Lays a picture out in a note of the given width; returns its size and the screen scale.</summary>
    (Size Shown, double Scale) LayOut(string file, double noteWidth) => OnSta(() =>
    {
        var picture = new PictureView { File = file };
        var host = new Grid { Width = noteWidth };
        host.Children.Add(picture);
        host.Measure(new Size(noteWidth, double.PositiveInfinity));
        host.Arrange(new Rect(host.DesiredSize));
        return (picture.RenderSize, VisualTreeHelper.GetDpi(picture).DpiScaleX);
    });

    [Fact]
    public void WidePicture_FillsTheNote_KeepingItsShape()
    {
        var store = new ImageStore(_dir);
        var file = store.FullPath(SavePng(store, 3000, 1500))!;
        foreach (var noteWidth in new[] { 250.0, 400, 700 })
        {
            var (shown, _) = LayOut(file, noteWidth);
            Assert.Equal(noteWidth, shown.Width, 0.5);                       // follows the note's width
            Assert.Equal((noteWidth - 2) / 2 + 2, shown.Height, 1.0);        // 2:1 kept (1px frame each side)
        }
    }

    [Fact]
    public void SmallPicture_IsNeverBlownUpPastItsOwnPixels()
    {
        var store = new ImageStore(_dir);
        var file = store.FullPath(SavePng(store, 120, 80))!;
        var (shown, scale) = LayOut(file, 600);
        Assert.Equal(120 / scale + 2, shown.Width, 0.5);                    // one picture pixel per screen pixel
        Assert.Equal(80 / scale + 2, shown.Height, 0.5);
    }

    [Fact]
    public void PictureFiles_AreNotKeptOpen()
    {
        var store = new ImageStore(_dir);
        var file = store.FullPath(SavePng(store, 64, 64))!;
        LayOut(file, 300);
        File.Delete(file); // would throw if the view still held the file
        Assert.False(File.Exists(file));
    }
}
