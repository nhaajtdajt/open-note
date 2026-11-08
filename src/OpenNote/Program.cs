using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using OpenNote.Core;
using OpenNote.Services;

namespace OpenNote;

/// <summary>
/// Where Open Note starts. The opennote-ai command (a console copy of this exe, or "--agent" as the
/// first argument) only hands its arguments to the running app over a pipe, so it runs without
/// starting WPF at all; everything else starts the app.
/// </summary>
internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        bool agent = string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath), AgentCommands.Command, StringComparison.OrdinalIgnoreCase);
        if (agent || args.FirstOrDefault() == "--agent")
        {
            var custom = ParseDataDirectory(args);
            var data = custom ?? DefaultDataDirectory();
            return AgentClient.Run(WithoutDataArgument(agent ? args : args[1..]), data, InstanceId(data), custom is not null);
        }
        return RunApp();
    }

    [MethodImpl(MethodImplOptions.NoInlining)] // App, and with it WPF, is only loaded on this path
    static int RunApp()
    {
        var app = new App();
        app.InitializeComponent();
        return app.Run();
    }

    /// <summary>
    /// %LOCALAPPDATA%\OpenNote. The app used to be called DeskNotes: its data folder is moved
    /// over once, so nobody loses notes when updating.
    /// </summary>
    internal static string DefaultDataDirectory()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var current = Path.Combine(local, "OpenNote");
        var legacy = Path.Combine(local, "DeskNotes");
        if (!Directory.Exists(current) && Directory.Exists(legacy))
        {
            // A DeskNotes still running would recreate its folder on its next save, stranding
            // those edits: ask it to save and quit first.
            var id = InstanceId(legacy);
            if (Mutex.TryOpenExisting($@"Local\DeskNotes-{id}", out var running))
            {
                using (running)
                {
                    if (EventWaitHandle.TryOpenExisting($@"Local\DeskNotes-exit-{id}", out var exit))
                    {
                        exit.Set();
                        exit.Dispose();
                    }
                    if (!WaitForExit(running))
                        return legacy;
                    running.ReleaseMutex();
                }
            }
            try
            {
                Directory.Move(legacy, current);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Another copy starting at the same moment may have just moved it: then use the new place.
                if (Directory.Exists(current) && !Directory.Exists(legacy))
                    return current;
                return legacy; // in use or locked: keep using it rather than start empty
            }
        }
        return current;
    }

    /// <summary>Waits (up to 10 s) until the running instance has saved and quit; true when it has.</summary>
    internal static bool WaitForExit(Mutex instance)
    {
        try
        {
            return instance.WaitOne(TimeSpan.FromSeconds(10));
        }
        catch (AbandonedMutexException)
        {
            return true; // it ended without releasing the mutex: gone all the same
        }
    }

    internal static string? ParseDataDirectory(string[] args)
    {
        int i = Array.IndexOf(args, "--data");
        // Without a trailing \, so "D:\notes" and "D:\notes\" are one data folder and one running copy.
        return i >= 0 && i + 1 < args.Length ? Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[i + 1])) : null;
    }

    /// <summary>The arguments without "--data <folder>" (already used to pick the data folder).</summary>
    internal static string[] WithoutDataArgument(string[] args)
    {
        int i = Array.IndexOf(args, "--data");
        return i < 0 ? args : [.. args[..i], .. args[Math.Min(i + 2, args.Length)..]];
    }

    internal static string InstanceId(string dataDirectory)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(dataDirectory.ToUpperInvariant()));
        return Convert.ToHexString(hash, 0, 8);
    }
}
