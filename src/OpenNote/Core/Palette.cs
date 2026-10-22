using System.Globalization;

namespace OpenNote.Core;

/// <summary>A note color: the paper itself and the slightly deeper "adhesive strip" across its top.</summary>
public sealed record NoteColor(string Name, string Hex, string StripHex);

public static class Palette
{
    // Soft, paper-like tones in the spirit of real sticky-note pads; the strip is the same hue, a step deeper.
    public static readonly IReadOnlyList<NoteColor> Colors =
    [
        new("Canary", "#FFF1A6", "#FBE07A"),
        new("Apricot", "#FFDDB8", "#FBC690"),
        new("Rose", "#FFD5E2", "#F9B7CC"),
        new("Lilac", "#E7DDFF", "#D3C3FB"),
        new("Sky", "#D2E9FF", "#B2D6FA"),
        new("Mint", "#D4F2DA", "#B3E4BF"),
        new("Index card", "#FBFBF8", "#E8E8E1"),
        new("Graphite", "#3B3F46", "#4B5059"),
    ];

    public static NoteColor Default => Colors[0];

    /// <summary>Parses #RRGGBB; anything unparsable falls back to the default canary.</summary>
    public static (byte R, byte G, byte B) ParseRgb(string? hex)
    {
        var s = (hex ?? "").Trim().TrimStart('#');
        if (s.Length == 6 && uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v))
            return ((byte)(v >> 16), (byte)(v >> 8), (byte)v);
        return (0xFF, 0xF1, 0xA6);
    }

    /// <summary>True when light text reads better than dark text on this background.</summary>
    public static bool IsDark(string hex)
    {
        var (r, g, b) = ParseRgb(hex);
        return 0.2126 * r + 0.7152 * g + 0.0722 * b < 140;
    }

    /// <summary>Strip color: the palette's tuned one, else derived (a little deeper than the paper).</summary>
    public static (byte R, byte G, byte B) Strip(string hex)
    {
        var known = Colors.FirstOrDefault(c => string.Equals(c.Hex, hex, StringComparison.OrdinalIgnoreCase));
        if (known is not null)
            return ParseRgb(known.StripHex);
        // Dark papers get a lighter strip (a darker one would vanish), light papers a deeper one.
        return IsDark(hex) ? Mix(ParseRgb(hex), (255, 255, 255), 0.1) : Mix(ParseRgb(hex), (0, 0, 0), 0.07);
    }

    /// <summary>"Ink": text color mixed from the paper's own hue, so text sits on the note naturally.</summary>
    public static (byte R, byte G, byte B) Ink(string hex) =>
        IsDark(hex) ? Mix(ParseRgb(hex), (255, 255, 255), 0.9) : Mix(ParseRgb(hex), (0, 0, 0), 0.84);

    /// <summary>Linear blend from <paramref name="a"/> (t = 0) to <paramref name="b"/> (t = 1).</summary>
    public static (byte R, byte G, byte B) Mix((byte R, byte G, byte B) a, (byte R, byte G, byte B) b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return (Lerp(a.R, b.R, t), Lerp(a.G, b.G, t), Lerp(a.B, b.B, t));
    }

    static byte Lerp(byte from, byte to, double t) => (byte)Math.Round(from + (to - from) * t);
}
