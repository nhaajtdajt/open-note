using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace OpenNote.Services;

/// <summary>
/// The "Open Note" entry in the Start menu, so the app can be found by typing its name. The
/// installer makes the same shortcut; this covers a copy that was just unzipped. A Store package
/// gets its entry from the package itself.
/// </summary>
internal static class StartMenuShortcut
{
    public static string Path => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Open Note.lnk");

    public static bool Exists => File.Exists(Path);

    /// <summary>Creates the shortcut, or points an existing one at this exe. False if it couldn't be written.</summary>
    public static bool Write() => Write(Path);

    internal static bool Write(string path)
    {
        if (Environment.ProcessPath is not { } exe)
            return false;
        IShellLinkW? link = null;
        try
        {
            // The shell's own shortcut object, called directly: no scripting host and no dynamic binder.
            link = (IShellLinkW)new ShellLink();
            link.SetPath(exe);
            link.SetWorkingDirectory(System.IO.Path.GetDirectoryName(exe) ?? "");
            link.SetDescription("Open Note: sticky notes for your desktop");
            link.SetIconLocation(exe, 0);
            ((IPersistFile)link).Save(path, true);
            return true;
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            if (link is not null)
                Marshal.FinalReleaseComObject(link);
        }
    }

    // Not sealed: a COM class is cast to the interfaces it implements at run time.
    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    class ShellLink;

    /// <summary>IShellLinkW. Every method is declared, in order, because COM calls go by position.</summary>
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
    interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int size, nint findData, uint flags);
        void GetIDList(out nint idList);
        void SetIDList(nint idList);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int size);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder dir, int size);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string dir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder args, int size);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string args);
        void GetHotkey(out short hotkey);
        void SetHotkey(short hotkey);
        void GetShowCmd(out int showCmd);
        void SetShowCmd(int showCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder iconPath, int size, out int icon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int icon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string relativePath, uint reserved);
        void Resolve(nint hwnd, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
    }
}
