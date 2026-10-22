using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace OpenNote.Native;

/// <summary>
/// Whether the app runs as a Microsoft Store (MSIX) package. Packaged apps start with Windows
/// through the package's StartupTask instead of the Run registry key, and their app-data writes
/// are redirected into the package's own folder.
/// </summary>
internal static class Packaging
{
    const int ErrorInsufficientBuffer = 122;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern int GetCurrentPackageFamilyName(ref int length, StringBuilder? name);

    static readonly Lazy<string?> FamilyName = new(() =>
    {
        try
        {
            int length = 0;
            if (GetCurrentPackageFamilyName(ref length, null) != ErrorInsufficientBuffer)
                return null; // APPMODEL_ERROR_NO_PACKAGE: a normal (unpackaged) app
            var name = new StringBuilder(length);
            return GetCurrentPackageFamilyName(ref length, name) == 0 ? name.ToString() : null;
        }
        catch (EntryPointNotFoundException)
        {
            return null;
        }
    });

    public static bool IsPackaged => FamilyName.Value is not null;

    /// <summary>
    /// Where data written to %LOCALAPPDATA%\<paramref name="folder"/> really lives, for opening
    /// it in Explorer (Explorer doesn't see the package's redirection).
    /// </summary>
    public static string RealLocalAppDataPath(string folder)
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return FamilyName.Value is { } family
            ? Path.Combine(local, "Packages", family, "LocalCache", "Local", folder)
            : Path.Combine(local, folder);
    }
}
