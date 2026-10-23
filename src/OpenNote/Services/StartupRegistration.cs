using Microsoft.Win32;

namespace OpenNote.Services;

/// <summary>"Start with Windows" via the per-user Run key (no admin rights, easy to undo).</summary>
internal static class StartupRegistration
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "OpenNote";

    static string Command => $"\"{Environment.ProcessPath}\"";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string s && s.Length > 0;
        }
    }

    const string LegacyValueName = "DeskNotes";

    /// <summary>
    /// Keeps the startup entry pointing at this exe (it may have moved, or been installed under
    /// the old name) and removes the entry of the old DeskNotes name.
    /// </summary>
    public static void Refresh()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        bool wasEnabled = key.GetValue(ValueName) is string s && s.Length > 0
            || key.GetValue(LegacyValueName) is string l && l.Length > 0;
        key.DeleteValue(LegacyValueName, throwOnMissingValue: false);
        if (wasEnabled)
            key.SetValue(ValueName, Command);
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        // Either way the old DeskNotes entry goes: it would start the old app next to this one.
        key.DeleteValue(LegacyValueName, throwOnMissingValue: false);
        if (enabled)
            key.SetValue(ValueName, Command);
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
