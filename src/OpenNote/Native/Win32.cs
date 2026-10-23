using System.Runtime.InteropServices;
using OpenNote.Core;

namespace OpenNote.Native;

internal static class Win32
{
    public const int GWL_STYLE = -16;
    public const int GWL_EXSTYLE = -20;
    public const long WS_MAXIMIZEBOX = 0x00010000;
    public const long WS_MINIMIZEBOX = 0x00020000;
    public const long WS_EX_TOOLWINDOW = 0x00000080;
    public const long WS_EX_APPWINDOW = 0x00040000;
    public const long WS_EX_TRANSPARENT = 0x00000020;
    public const long WS_EX_NOACTIVATE = 0x08000000;

    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_FRAMECHANGED = 0x0020;

    public static readonly nint HWND_TOPMOST = -1;
    public static readonly nint HWND_NOTOPMOST = -2;
    public static readonly nint HWND_MESSAGE = -3;

    public const int WM_HOTKEY = 0x0312;
    public const uint MOD_NOREPEAT = 0x4000;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
        public readonly PixelRect ToPixelRect() => new(Left, Top, Right - Left, Bottom - Top);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X, Y;
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    public static extern nint GetWindowLongPtr(nint hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    public static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(nint hWnd, out RECT rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out POINT point);

    /// <summary>The system's double-click time in milliseconds.</summary>
    [DllImport("user32.dll")]
    public static extern uint GetDoubleClickTime();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterHotKey(nint hWnd, int id);

    /// <summary>
    /// Hides a window from Alt+Tab and the taskbar (tool window, never an app window) and
    /// removes maximize/minimize so Win+Up or snapping can't blow a note up to full screen.
    /// </summary>
    public static void MakeNoteWindow(nint hwnd)
    {
        long ex = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        ex = (ex | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW;
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, (nint)ex);

        long style = GetWindowLongPtr(hwnd, GWL_STYLE);
        style &= ~(WS_MAXIMIZEBOX | WS_MINIMIZEBOX);
        SetWindowLongPtr(hwnd, GWL_STYLE, (nint)style);

        SetWindowPos(hwnd, 0, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
    }

    /// <summary>
    /// An overlay the mouse passes straight through and that never takes focus: not in Alt+Tab,
    /// not on the taskbar, and invisible to clicks.
    /// </summary>
    public static void MakeOverlayWindow(nint hwnd)
    {
        long ex = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        ex = (ex | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT) & ~WS_EX_APPWINDOW;
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, (nint)ex);
        SetWindowPos(hwnd, 0, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
    }

    /// <summary>Moves a window to <paramref name="x"/>, <paramref name="y"/> and puts it right behind <paramref name="behind"/>.</summary>
    public static void MoveBehind(nint hwnd, nint behind, int x, int y, int width = 0, int height = 0)
    {
        uint flags = SWP_NOACTIVATE | (width == 0 ? SWP_NOSIZE : 0);
        SetWindowPos(hwnd, behind, x, y, width, height, flags);
    }

    /// <summary>
    /// Raises a non-topmost window to the top of the normal band without activating it.
    /// (HWND_NOTOPMOST alone does nothing for a window that is already non-topmost.)
    /// </summary>
    public static void RaiseToTop(nint hwnd)
    {
        const uint flags = SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE;
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, flags);
        SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, flags);
    }

    public static PixelRect? GetBounds(nint hwnd) =>
        GetWindowRect(hwnd, out var r) ? r.ToPixelRect() : null;

    public static void SetBounds(nint hwnd, PixelRect r, bool moveOnly = false)
    {
        uint flags = SWP_NOZORDER | SWP_NOACTIVATE | (moveOnly ? SWP_NOSIZE : 0);
        SetWindowPos(hwnd, 0, r.X, r.Y, r.Width, r.Height, flags);
    }

    public static PixelPoint CursorPosition() =>
        GetCursorPos(out var p) ? new PixelPoint(p.X, p.Y) : default;
}
