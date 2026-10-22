using System.Runtime.InteropServices;
using OpenNote.Core;

namespace OpenNote.Native;

internal static class Displays
{
    const uint MONITORINFOF_PRIMARY = 1;

    [StructLayout(LayoutKind.Sequential)]
    struct MONITORINFO
    {
        public int cbSize;
        public Win32.RECT rcMonitor;
        public Win32.RECT rcWork;
        public uint dwFlags;
    }

    delegate bool MonitorEnumProc(nint hMonitor, nint hdc, nint lprcMonitor, nint data);

    [DllImport("user32.dll")]
    static extern bool EnumDisplayMonitors(nint hdc, nint clip, MonitorEnumProc callback, nint data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern bool GetMonitorInfoW(nint hMonitor, ref MONITORINFO info);

    /// <summary>Work areas (screen minus taskbar) of all monitors, primary first, in physical pixels (the process is per-monitor DPI aware).</summary>
    public static IReadOnlyList<PixelRect> WorkAreas()
    {
        var areas = new List<(PixelRect Area, bool Primary)>();
        EnumDisplayMonitors(0, 0, (monitor, _, _, _) =>
        {
            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfoW(monitor, ref info))
                areas.Add((info.rcWork.ToPixelRect(), (info.dwFlags & MONITORINFOF_PRIMARY) != 0));
            return true;
        }, 0);
        return areas.OrderByDescending(a => a.Primary).Select(a => a.Area).ToList();
    }
}
