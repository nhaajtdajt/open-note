using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Windows.Threading;
using OpenNote.Core;
using OpenNote.Views;

namespace OpenNote.Services;

/// <summary>One opennote-ai command, sent from the command to the running app.</summary>
internal sealed record AgentRequest(string[] Args, string Cwd);

internal sealed record AgentReply(int Code, string Output);

/// <summary>
/// The running app's end of the opennote-ai command: a named pipe that only this Windows user
/// can open. Each connection carries one command, run on the UI thread against the notes, so
/// what an agent writes goes through the same save path as typing (nothing writes notes.json behind the app's back).
/// </summary>
internal sealed class AgentServer : IAgentNotes, IDisposable
{
    readonly NoteManager _notes;
    readonly string _dataDirectory;
    readonly string _pipeName;
    readonly Dispatcher _dispatcher;
    readonly Action<string> _openFile;
    readonly CancellationTokenSource _stop = new();

    /// <summary>First word of the request a second launch sends to open a note file (not an opennote-ai command).</summary>
    internal const string OpenFileRequest = "--open-file";

    /// <param name="openFile">Opens an .onote file as a new note (a file double-clicked while Open Note runs).</param>
    public AgentServer(NoteManager notes, string dataDirectory, string instanceId, Dispatcher dispatcher, Action<string> openFile)
    {
        _openFile = openFile;
        _notes = notes;
        _dataDirectory = dataDirectory;
        _pipeName = AgentClient.PipeName(instanceId);
        _dispatcher = dispatcher;
        _ = Task.Run(ListenAsync);
    }

    public void Dispose() => _stop.Cancel();

    async Task ListenAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                // One pipe, reused for every command: making a new one while the command still holds
                // the old one fails, and left the app deaf for half a second after each command.
                using var pipe = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                while (true)
                {
                    await pipe.WaitForConnectionAsync(_stop.Token);
                    using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token))
                    {
                        timeout.CancelAfter(TimeSpan.FromSeconds(30)); // a client that never finishes its request can't block the next one
                        await ServeAsync(pipe, timeout.Token);
                    }
                    pipe.Disconnect();
                }
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or JsonException or UnauthorizedAccessException or InvalidOperationException)
            {
                // A client that broke off, or the pipe name is taken: wait a moment, then listen again.
                try
                {
                    await Task.Delay(500, _stop.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    async Task ServeAsync(NamedPipeServerStream pipe, CancellationToken token)
    {
        using var reader = new StreamReader(pipe, new UTF8Encoding(false), leaveOpen: true);
        var request = JsonSerializer.Deserialize<AgentRequest>(await reader.ReadLineAsync(token) ?? "") ?? new AgentRequest([], "");
        AgentReply reply;
        try
        {
            if (request.Args is [OpenFileRequest, var file])
            {
                await _dispatcher.InvokeAsync(() => _openFile(file));
                reply = new AgentReply(0, "");
            }
            else
            {
                var (code, output) = await _dispatcher.InvokeAsync(() => AgentCommands.Run(request.Args ?? [], request.Cwd ?? "", this));
                reply = new AgentReply(code, output);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            reply = new AgentReply(1, "Open Note couldn't do that: " + ex.Message);
        }
        await pipe.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(reply) + "\n"), token);
        await pipe.FlushAsync(token);
        // Wait until the command has read the answer and hung up (closing first would drop the answer),
        // but only until the deadline: a client that never reads can't keep the pipe from the next one.
        try
        {
            var rest = new byte[64];
            while (await pipe.ReadAsync(rest, token) > 0)
            {
            }
        }
        catch (IOException)
        {
            // The command hung up: done.
        }
    }

    // ---------- What the commands can do (called on the UI thread) ----------

    public IReadOnlyList<Note> Notes => _notes.Notes;
    public AppSettings Settings => _notes.Settings;
    public string? PicturePath(string relativePath) => _notes.Images.FullPath(relativePath);
    public string SavePicture(string file) => NoteWindow.SavePictureFile(_notes.Images, file);
    public Note Create(string heading, string text, string color) => _notes.CreateNote(text: text, heading: heading, color: color, activate: false);
    public void Append(Note note, string text) => _notes.AppendText(note, text);
    public string? SaveStyle(string style) => _notes.Settings.Update(_dataDirectory, x => x.NoteStyle = style);
}

/// <summary>
/// The opennote-ai command itself (OpenNote.exe started as opennote-ai.exe, a console copy): sends
/// its arguments to the running app, starting the app first if needed, and prints the answer.
/// </summary>
internal static class AgentClient
{
    static readonly TimeSpan StartWait = TimeSpan.FromSeconds(15);

    /// <summary>The running app's pipe for this data folder (kept here so the command needs nothing from WPF).</summary>
    public static string PipeName(string instanceId) => $"OpenNote-agent-{instanceId}";

    public static int Run(string[] args, string dataDirectory, string instanceId, bool customDataDirectory)
    {
        try
        {
            args = ReadStdinValues(args);
            using var pipe = Connect(dataDirectory, instanceId, customDataDirectory);
            var request = JsonSerializer.Serialize(new AgentRequest(args, Environment.CurrentDirectory)) + "\n";
            pipe.Write(Encoding.UTF8.GetBytes(request));
            pipe.Flush();
            using var reader = new StreamReader(pipe, new UTF8Encoding(false));
            var reply = JsonSerializer.Deserialize<AgentReply>(reader.ReadLine() ?? "")
                ?? throw new IOException("Open Note gave no answer.");
            Print(reply.Output, reply.Code == 0 ? Console.OpenStandardOutput : Console.OpenStandardError, reply.Code == 0);
            return reply.Code;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or JsonException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            Print(ex.Message, Console.OpenStandardError, toOut: false);
            return 1;
        }
    }

    /// <summary>Hands a note file to the running app (a second launch, e.g. a double-click on an .onote file). False if it doesn't answer.</summary>
    public static bool SendOpenFile(string instanceId, string file)
    {
        try
        {
            using var pipe = TryConnect(instanceId, TimeSpan.FromSeconds(10));
            if (pipe is null)
                return false;
            pipe.Write(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new AgentRequest([AgentServer.OpenFileRequest, file], "")) + "\n"));
            pipe.Flush();
            using var reader = new StreamReader(pipe, new UTF8Encoding(false));
            return reader.ReadLine() is not null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }

    /// <summary>"--text -" (or "--set -") reads the value from standard input, for text too long or awkward to quote.</summary>
    static string[] ReadStdinValues(string[] args)
    {
        var result = (string[])args.Clone();
        for (int i = 0; i + 1 < result.Length; i++)
        {
            if (result[i] is "--text" or "--set" && result[i + 1] == "-")
            {
                using var input = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false));
                result[i + 1] = input.ReadToEnd();
            }
        }
        return result;
    }

    static NamedPipeClientStream Connect(string dataDirectory, string instanceId, bool customDataDirectory)
    {
        if (TryConnect(instanceId, TimeSpan.FromMilliseconds(500)) is { } pipe)
            return pipe;

        bool running = Mutex.TryOpenExisting($@"Local\OpenNote-{instanceId}", out var instance);
        instance?.Dispose();
        if (!running)
        {
            var exe = Path.Combine(AppContext.BaseDirectory, "OpenNote.exe");
            // Through the shell, so Open Note doesn't inherit this command's output: a terminal or an
            // agent reading it would otherwise wait until Open Note quits. (A folder name can't hold
            // a quote; a final \ is doubled so it doesn't escape the closing quote.)
            var start = new ProcessStartInfo(exe) { UseShellExecute = true };
            if (customDataDirectory)
                start.Arguments = $"--data \"{dataDirectory}{(dataDirectory.EndsWith('\\') ? "\\" : "")}\"";
            Process.Start(start)?.Dispose();
        }

        var until = DateTime.UtcNow + StartWait;
        while (DateTime.UtcNow < until)
        {
            if (TryConnect(instanceId, TimeSpan.FromSeconds(1)) is { } started)
                return started;
        }
        throw new TimeoutException(running
            ? "Open Note is running but doesn't answer. If it was just updated, quit it (tray icon → Quit) and start it again."
            : "Open Note didn't start in time. If it shows a window (first start), finish it, then run the command again.");
    }

    static NamedPipeClientStream? TryConnect(string instanceId, TimeSpan timeout)
    {
        var pipe = new NamedPipeClientStream(".", PipeName(instanceId), PipeDirection.InOut, PipeOptions.CurrentUserOnly);
        try
        {
            pipe.Connect(timeout);
            return pipe;
        }
        catch (TimeoutException)
        {
            pipe.Dispose();
            return null;
        }
    }

    /// <summary>
    /// UTF-8 so any language comes through. A real console gets its code page switched to UTF-8;
    /// an agent reading through a pipe gets the bytes as they are.
    /// </summary>
    static void Print(string text, Func<Stream> open, bool toOut)
    {
        if (!text.EndsWith('\n'))
            text += Environment.NewLine;
        bool redirected = toOut ? Console.IsOutputRedirected : Console.IsErrorRedirected;
        if (!redirected)
        {
            try
            {
                Console.OutputEncoding = new UTF8Encoding(false);
            }
            catch (IOException)
            {
                // No console after all: write the bytes below.
            }
        }
        using var stream = open();
        stream.Write(Encoding.UTF8.GetBytes(text));
        stream.Flush();
    }
}
