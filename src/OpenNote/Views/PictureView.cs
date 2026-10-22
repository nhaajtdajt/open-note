using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace OpenNote.Views;

/// <summary>
/// A picture inside a note that follows the note's width without ever looking broken:
/// <list type="bullet">
/// <item>it fills the note's width, keeping its proportions, but never grows past its own
/// pixel size (one picture pixel per screen pixel), so it is never blown up into blur;</item>
/// <item>it is decoded at the size actually shown (in physical pixels), and decoded again,
/// sharper, in the background when the note is made wider: no blur, no wasted memory;</item>
/// <item>phone photos are turned upright using their EXIF orientation;</item>
/// <item>corners are clipped to match the rounded frame.</item>
/// </list>
/// <para>
/// This type is public with a parameterless constructor on purpose. When a picture is deleted
/// from a note, WPF saves a XAML copy of it so Ctrl+Z can bring it back; it can only do that for
/// a public type it can create again, and only from its public properties
/// (<see cref="File"/>, <see cref="FitWidth"/>). Its inner Border and Image are private visuals,
/// rebuilt on load, so nothing else needs to be serialized.
/// </para>
/// </summary>
public sealed class PictureView : FrameworkElement
{
    const double Radius = 6;
    const int DecodeStep = 128;           // re-decode in steps, not on every pixel of a resize
    const int FirstDecodeWidth = 480;     // quick first pass so the note opens with the right shape

    static readonly Brush NormalBorder = Frozen(new SolidColorBrush(Color.FromArgb(0x26, 0, 0, 0)));
    static readonly Brush SelectedBorder = Frozen(new SolidColorBrush(Color.FromRgb(0x33, 0x88, 0xFF)));

    readonly Border _frame = new()
    {
        CornerRadius = new CornerRadius(Radius),
        BorderThickness = new Thickness(1),
        BorderBrush = NormalBorder,
        SnapsToDevicePixels = true,
    };
    readonly Image _image = new() { Stretch = Stretch.Uniform };
    readonly DispatcherTimer _resizeDebounce;
    string _file = "";
    int _orientation = 1;                 // EXIF 1..8
    int _pixelWidth;                      // upright size of the full picture
    int _decodedWidth;
    int _wantedWidth;
    bool _decoding;
    bool _selected;
    double _fitWidth = double.PositiveInfinity;

    public PictureView()
    {
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.HighQuality);
        _frame.Child = _image;
        AddVisualChild(_frame);
        HorizontalAlignment = HorizontalAlignment.Left;
        Margin = new Thickness(0, 3, 0, 3);
        Cursor = System.Windows.Input.Cursors.Hand;

        _resizeDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _resizeDebounce.Tick += (_, _) =>
        {
            _resizeDebounce.Stop();
            Sharpen();
        };
        _image.SizeChanged += (_, e) =>
        {
            _image.Clip = new RectangleGeometry(new Rect(e.NewSize), Radius - 1, Radius - 1);
            _resizeDebounce.Stop();
            _resizeDebounce.Start();
        };
        Loaded += (_, _) => Sharpen();
    }

    /// <summary>Full path of the picture file. Setting it loads the picture.</summary>
    public string File
    {
        get => _file;
        set
        {
            if (_file == value)
                return;
            _file = value;
            Load();
        }
    }

    /// <summary>Path as stored in the note ("images/….png").</summary>
    public string RelativePath => $"images/{Path.GetFileName(_file)}";

    /// <summary>
    /// Width of the note's text area. A picture sitting inside a line of text isn't told how wide
    /// the line is, so the note tells it (and again whenever the note is resized).
    /// </summary>
    public double FitWidth
    {
        get => _fitWidth;
        set
        {
            if (Math.Abs(value - _fitWidth) < 0.5)
                return;
            _fitWidth = value;
            InvalidateMeasure();
        }
    }

    /// <summary>Marks the picture as part of the selection (a blue frame).</summary>
    public bool IsSelected
    {
        get => _selected;
        set
        {
            if (_selected == value)
                return;
            _selected = value;
            _frame.BorderThickness = new Thickness(value ? 2 : 1);
            _frame.BorderBrush = value ? SelectedBorder : NormalBorder;
        }
    }

    /// <summary>Never wider than the picture's own pixels on this monitor (plus the frame).</summary>
    internal double NaturalWidth =>
        _pixelWidth > 0 ? _pixelWidth / VisualTreeHelper.GetDpi(this).DpiScaleX + 2 : double.PositiveInfinity;

    // ---------- Layout ----------

    protected override int VisualChildrenCount => 1;

    protected override Visual GetVisualChild(int index) =>
        index == 0 ? _frame : throw new ArgumentOutOfRangeException(nameof(index));

    protected override Size MeasureOverride(Size constraint)
    {
        _frame.Measure(constraint with { Width = Math.Min(Math.Min(constraint.Width, NaturalWidth), FitWidth) });
        return _frame.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _frame.Arrange(new Rect(finalSize));
        return finalSize;
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        InvalidateMeasure(); // the same picture is a different number of DIPs on another monitor
        Sharpen();
    }

    // ---------- Loading ----------

    void Load()
    {
        _decodedWidth = _wantedWidth = 0;
        try
        {
            (_pixelWidth, _, _orientation) = ReadHeader(_file);
            // Small synchronous first decode: the note opens at its final layout, then sharpens.
            _decodedWidth = Math.Min(_pixelWidth, FirstDecodeWidth);
            _image.Source = Decode(_file, _decodedWidth, _pixelWidth, _orientation);
            _frame.Child = _image;
        }
        catch (Exception ex) when (IsUnreadable(ex))
        {
            // Keep the picture (and its reference) even if the file is gone or damaged, so nothing is silently dropped.
            _pixelWidth = 0;
            _frame.Child = new TextBlock
            {
                Text = ex is FileNotFoundException or DirectoryNotFoundException ? "[picture not found]" : "[picture can't be shown]",
                Opacity = 0.5,
                Margin = new Thickness(6, 3, 6, 3),
            };
        }
        InvalidateMeasure();
    }

    /// <summary>Decodes again at a higher resolution if the picture is now shown larger than decoded.</summary>
    void Sharpen()
    {
        if (_pixelWidth == 0 || _image.ActualWidth <= 0)
            return;
        int shownPixels = (int)Math.Ceiling(_image.ActualWidth * VisualTreeHelper.GetDpi(this).DpiScaleX);
        int target = Math.Min(_pixelWidth, (shownPixels + DecodeStep - 1) / DecodeStep * DecodeStep);
        if (target <= _decodedWidth)
            return;
        _wantedWidth = Math.Max(_wantedWidth, target);
        if (!_decoding)
            DecodeInBackground();
    }

    async void DecodeInBackground()
    {
        _decoding = true;
        string file = _file;
        while (_wantedWidth > _decodedWidth && file == _file)
        {
            int width = _wantedWidth;
            try
            {
                var (full, orientation) = (_pixelWidth, _orientation);
                var source = await Task.Run(() => Decode(file, width, full, orientation));
                if (file != _file)
                    break;
                _image.Source = source;
                _decodedWidth = width;
            }
            catch (Exception ex) when (IsUnreadable(ex))
            {
                break; // keep the picture we already have
            }
        }
        _decoding = false;
    }

    /// <summary>What a missing, locked or damaged picture file throws while it is read or decoded.</summary>
    static bool IsUnreadable(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or NotSupportedException or FormatException
            or OverflowException or ArgumentException or InvalidOperationException
            or System.Runtime.InteropServices.COMException;

    /// <summary>Reads size and EXIF orientation without decoding the pixels.</summary>
    static (int Width, int Height, int Orientation) ReadHeader(string file)
    {
        try
        {
            using var stream = System.IO.File.OpenRead(file);
            var frame = BitmapFrame.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            int orientation = 1;
            try
            {
                if (frame.Metadata is BitmapMetadata meta && meta.GetQuery("System.Photo.Orientation") is ushort value and >= 1 and <= 8)
                    orientation = value;
            }
            catch (Exception ex) when (ex is NotSupportedException or ArgumentException or InvalidOperationException)
            {
                // No EXIF block (PNG screenshots, most web images): already upright.
            }
            bool sideways = orientation >= 5;
            return sideways ? (frame.PixelHeight, frame.PixelWidth, orientation) : (frame.PixelWidth, frame.PixelHeight, orientation);
        }
        catch (Exception ex) when (ex is NotSupportedException or FileFormatException or ArgumentException)
        {
            throw new IOException($"Not a readable picture: {Path.GetFileName(file)}", ex);
        }
    }

    /// <summary>
    /// Decodes so the upright picture is <paramref name="width"/> pixels wide (the decoder scales
    /// while decoding, which is both faster and better-looking than shrinking afterwards).
    /// </summary>
    static BitmapSource Decode(string file, int width, int fullWidth, int orientation)
    {
        bool sideways = orientation >= 5;
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(file);
        bitmap.CacheOption = BitmapCacheOption.OnLoad; // read now, release the file
        bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        if (width < fullWidth)
        {
            // For a sideways photo the stored image's height becomes the shown width.
            if (sideways)
                bitmap.DecodePixelHeight = width;
            else
                bitmap.DecodePixelWidth = width;
        }
        bitmap.EndInit();
        bitmap.Freeze();

        if (orientation == 1)
            return bitmap;
        var upright = new TransformedBitmap(bitmap, OrientationTransform(orientation));
        upright.Freeze();
        return upright;
    }

    static Transform OrientationTransform(int orientation) => orientation switch
    {
        2 => new ScaleTransform(-1, 1),
        3 => new RotateTransform(180),
        4 => new ScaleTransform(1, -1),
        5 => new TransformGroup { Children = { new RotateTransform(90), new ScaleTransform(-1, 1) } },
        6 => new RotateTransform(90),
        7 => new TransformGroup { Children = { new RotateTransform(270), new ScaleTransform(-1, 1) } },
        8 => new RotateTransform(270),
        _ => Transform.Identity,
    };

    static Brush Frozen(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }
}
