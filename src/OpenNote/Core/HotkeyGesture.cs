namespace OpenNote.Core;

/// <summary>A global shortcut such as "Ctrl+Alt+N", in the form RegisterHotKey expects.</summary>
public readonly record struct HotkeyGesture(uint Modifiers, uint VirtualKey)
{
    public const uint ModAlt = 0x1, ModControl = 0x2, ModShift = 0x4, ModWin = 0x8;

    static readonly Dictionary<string, uint> NamedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Space"] = 0x20, ["Enter"] = 0x0D, ["Tab"] = 0x09, ["Esc"] = 0x1B, ["Escape"] = 0x1B,
        ["Backspace"] = 0x08, ["Insert"] = 0x2D, ["Delete"] = 0x2E, ["Del"] = 0x2E,
        ["Home"] = 0x24, ["End"] = 0x23, ["PageUp"] = 0x21, ["PageDown"] = 0x22,
        ["Left"] = 0x25, ["Up"] = 0x26, ["Right"] = 0x27, ["Down"] = 0x28,
        ["Pause"] = 0x13, ["`"] = 0xC0, ["-"] = 0xBD, ["="] = 0xBB, [";"] = 0xBA,
        ["/"] = 0xBF, ["["] = 0xDB, ["]"] = 0xDD, ["'"] = 0xDE, [","] = 0xBC, ["."] = 0xBE,
    };

    /// <summary>
    /// Parses "Ctrl+Alt+N" style text. At least one modifier is required so a global
    /// shortcut can never swallow a plain key you type in other apps.
    /// </summary>
    public static bool TryParse(string? text, out HotkeyGesture gesture)
    {
        gesture = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return false;

        uint mods = 0;
        foreach (var p in parts[..^1])
        {
            uint m = p.ToLowerInvariant() switch
            {
                "ctrl" or "control" => ModControl,
                "alt" => ModAlt,
                "shift" => ModShift,
                "win" or "windows" => ModWin,
                _ => 0,
            };
            if (m == 0 || (mods & m) != 0)
                return false;
            mods |= m;
        }

        if (!TryParseKey(parts[^1], out var vk))
            return false;
        gesture = new HotkeyGesture(mods, vk);
        return true;
    }

    static bool TryParseKey(string key, out uint vk)
    {
        vk = 0;
        if (key.Length == 1 && char.IsAsciiLetterOrDigit(key[0]))
        {
            vk = char.ToUpperInvariant(key[0]);
            return true;
        }
        if (key.Length is 2 or 3 && (key[0] is 'F' or 'f') && int.TryParse(key[1..], out var f) && f is >= 1 and <= 24)
        {
            vk = (uint)(0x70 + f - 1);
            return true;
        }
        return NamedKeys.TryGetValue(key, out vk);
    }
}
