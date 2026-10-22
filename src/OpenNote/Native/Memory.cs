using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;

namespace OpenNote.Native;

internal static class Memory
{
    [DllImport("kernel32.dll", EntryPoint = "K32EmptyWorkingSet")]
    static extern bool EmptyWorkingSet(nint process);

    /// <summary>
    /// Compacts the managed heap and hands idle pages back to Windows. Called only when the app
    /// goes quiet (after startup, when you switch to another app, after closing the notes list),
    /// so the cost of paging back in is paid rarely and only by the note you touch next.
    /// </summary>
    public static void Trim()
    {
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        using var self = Process.GetCurrentProcess();
        EmptyWorkingSet(self.Handle);
    }
}
