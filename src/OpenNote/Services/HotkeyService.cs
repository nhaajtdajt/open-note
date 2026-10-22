using System.Windows.Interop;
using OpenNote.Core;
using OpenNote.Native;

namespace OpenNote.Services;

/// <summary>System-wide shortcuts via RegisterHotKey on a hidden message-only window.</summary>
internal sealed class HotkeyService : IDisposable
{
    readonly HwndSource _source;
    readonly Dictionary<int, Action> _actions = [];
    int _nextId = 1;

    public HotkeyService()
    {
        _source = new HwndSource(new HwndSourceParameters("OpenNoteHotkeys")
        {
            ParentWindow = Win32.HWND_MESSAGE,
            WindowStyle = 0,
        });
        _source.AddHook(WndProc);
    }

    /// <summary>
    /// Registers a shortcut. Returns null on success (or when <paramref name="gesture"/> is empty,
    /// meaning disabled), otherwise a short reason it failed.
    /// </summary>
    public string? Register(string gesture, Action action)
    {
        if (string.IsNullOrWhiteSpace(gesture))
            return null;
        if (!HotkeyGesture.TryParse(gesture, out var g))
            return $"{gesture}: not a valid shortcut";

        int id = _nextId++;
        if (!Win32.RegisterHotKey(_source.Handle, id, g.Modifiers | Win32.MOD_NOREPEAT, g.VirtualKey))
            return $"{gesture}: already used by another app";

        _actions[id] = action;
        return null;
    }

    public void UnregisterAll()
    {
        foreach (var id in _actions.Keys)
            Win32.UnregisterHotKey(_source.Handle, id);
        _actions.Clear();
    }

    nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == Win32.WM_HOTKEY && _actions.TryGetValue((int)wParam, out var action))
        {
            handled = true;
            action();
        }
        return 0;
    }

    public void Dispose()
    {
        UnregisterAll();
        _source.Dispose();
    }
}
