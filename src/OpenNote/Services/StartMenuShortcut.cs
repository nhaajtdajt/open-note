using System.IO;
using System.Runtime.InteropServices;

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
    public static bool Write()
    {
        if (Environment.ProcessPath is not { } exe)
            return false;
        object? shell = null, link = null;
        try
        {
            // The Windows scripting host creates .lnk files; used late-bound, so no extra library is needed.
            if (Type.GetTypeFromProgID("WScript.Shell") is not { } type)
                return false;
            shell = Activator.CreateInstance(type)!;
            dynamic s = shell;
            link = s.CreateShortcut(Path);
            dynamic l = link;
            l.TargetPath = exe;
            l.WorkingDirectory = System.IO.Path.GetDirectoryName(exe) ?? "";
            l.Description = "Open Note: sticky notes for your desktop";
            l.IconLocation = exe + ",0";
            l.Save();
            return true;
        }
        catch (Exception ex) when (ex is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException
                                       or IOException or UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            Release(link);
            Release(shell);
        }
    }

    static void Release(object? com)
    {
        if (com is not null && Marshal.IsComObject(com))
            Marshal.FinalReleaseComObject(com);
    }
}
