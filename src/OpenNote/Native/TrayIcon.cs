using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace OpenNote.Native;

/// <summary>
/// A notification-area icon via Shell_NotifyIcon, so the app doesn't have to load WinForms
/// just for NotifyIcon. Re-adds itself when Explorer restarts.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    const int WM_APP_TRAY = 0x8000 + 1;
    const int WM_LBUTTONUP = 0x0202, WM_RBUTTONUP = 0x0205, WM_CONTEXTMENU = 0x007B;
    const uint NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2;
    const uint NIF_MESSAGE = 0x1, NIF_ICON = 0x2, NIF_TIP = 0x4, NIF_INFO = 0x10;
    const uint NIIF_INFO = 0x1, NIIF_WARNING = 0x2;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct NOTIFYICONDATAW
    {
        public int cbSize;
        public nint hWnd;
        public uint uID;
        public uint uFlags;
        public int uCallbackMessage;
        public nint hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public nint hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern bool Shell_NotifyIconW(uint message, ref NOTIFYICONDATAW data);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern uint ExtractIconExW(string file, int index, nint[]? large, nint[] small, uint count);

    [DllImport("user32.dll")]
    static extern bool DestroyIcon(nint icon);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int RegisterWindowMessageW(string name);

    readonly HwndSource _window;
    readonly nint _icon;
    readonly string _tip;
    readonly int _taskbarCreated = RegisterWindowMessageW("TaskbarCreated");

    public event Action? LeftClick;
    public event Action? RightClick;

    public TrayIcon(string tip)
    {
        _tip = tip;
        _window = new HwndSource(new HwndSourceParameters("OpenNoteTray") { WindowStyle = 0, Width = 0, Height = 0 });
        _window.AddHook(WndProc);

        // The exe's own icon (embedded via ApplicationIcon), at the small-icon size.
        var small = new nint[1];
        ExtractIconExW(Environment.ProcessPath ?? "", 0, null, small, 1);
        _icon = small[0];
        Add();
    }

    /// <summary>Handle of the tray's hidden window, used to take foreground before showing a menu.</summary>
    public nint Handle => _window.Handle;

    public void ShowWarning(string title, string text) => ShowBalloon(title, text, NIIF_WARNING);

    public void ShowInfo(string title, string text) => ShowBalloon(title, text, NIIF_INFO);

    void ShowBalloon(string title, string text, uint icon)
    {
        var data = Data(NIF_INFO);
        data.szInfoTitle = Truncate(title, 63);
        data.szInfo = Truncate(text, 255);
        data.dwInfoFlags = icon;
        Shell_NotifyIconW(NIM_MODIFY, ref data);
    }

    void Add()
    {
        var data = Data(NIF_MESSAGE | NIF_ICON | NIF_TIP);
        Shell_NotifyIconW(NIM_ADD, ref data);
    }

    NOTIFYICONDATAW Data(uint flags) => new()
    {
        cbSize = Marshal.SizeOf<NOTIFYICONDATAW>(),
        hWnd = _window.Handle,
        uID = 1,
        uFlags = flags,
        uCallbackMessage = WM_APP_TRAY,
        hIcon = _icon,
        szTip = _tip,
        szInfo = "",
        szInfoTitle = "",
    };

    nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WM_APP_TRAY)
        {
            switch ((int)lParam & 0xFFFF)
            {
                case WM_LBUTTONUP: LeftClick?.Invoke(); break;
                case WM_RBUTTONUP or WM_CONTEXTMENU: RightClick?.Invoke(); break;
            }
            handled = true;
        }
        else if (msg == _taskbarCreated)
        {
            Add(); // Explorer restarted and lost our icon
        }
        return 0;
    }

    static string Truncate(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";

    public void Dispose()
    {
        var data = Data(0);
        Shell_NotifyIconW(NIM_DELETE, ref data);
        if (_icon != 0)
            DestroyIcon(_icon);
        _window.Dispose();
    }
}
