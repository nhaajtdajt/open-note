using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace OpenNote.Services;

/// <summary>
/// Reads text aloud with the voices installed in Windows (SAPI), fully offline. The speech
/// engine is created when you start reading and released as soon as it is done, so it costs
/// nothing the rest of the time. It is called late-bound through COM automation (no extra
/// library), with plain reflection rather than <c>dynamic</c>, which would load the C# binder.
/// </summary>
internal sealed class SpeechService
{
    const int SpeakAsync = 1, SpeakPurgeBeforeSpeak = 2;
    const int StateDone = 1;
    const int MaxChars = 1500;

    object? _voice;
    DispatcherTimer? _poll;

    public bool IsSpeaking => _voice is not null;

    /// <summary>
    /// Starts reading <paramref name="text"/> with a voice for <paramref name="language"/>.
    /// Returns null on success, or a short message for the user when it can't be done.
    /// </summary>
    public string? Speak(string text, Core.Language language)
    {
        Stop();
        text = text.Trim();
        if (text.Length == 0)
            return Core.Ui.T("There is nothing to read.");
        if (text.Length > MaxChars)
            text = text[..MaxChars];

        try
        {
            var type = Type.GetTypeFromProgID("SAPI.SpVoice");
            if (type is null)
                return Core.Ui.T("Windows speech isn't available on this PC.");
            var voice = Activator.CreateInstance(type)!;

            var chosen = FindVoice(voice, language.VoiceId);
            if (chosen is null)
            {
                Release(voice);
                return Core.Ui.T("No {0} voice is installed. Add one in Settings → Time & language → Speech.", Core.Ui.T(language.EnglishName));
            }
            _voice = voice; // so Stop() releases it if speaking fails
            try
            {
                Invoke(voice, "Voice", BindingFlags.PutRefDispProperty, chosen);
            }
            finally
            {
                Release(chosen); // the engine keeps its own reference
            }
            Invoke(voice, "Speak", BindingFlags.InvokeMethod, text, SpeakAsync | SpeakPurgeBeforeSpeak);

            _poll ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _poll.Tick -= PollTick;
            _poll.Tick += PollTick;
            _poll.Start();
            return null;
        }
        catch (Exception ex) when (IsComFailure(ex))
        {
            Stop();
            return Core.Ui.T("Couldn't read this text aloud.");
        }
    }

    /// <summary>Stops reading and frees the speech engine.</summary>
    public void Stop()
    {
        _poll?.Stop();
        if (_voice is null)
            return;
        var voice = _voice;
        _voice = null;
        try
        {
            Invoke(voice, "Speak", BindingFlags.InvokeMethod, "", SpeakPurgeBeforeSpeak); // cut off whatever is being read
        }
        catch (Exception ex) when (IsComFailure(ex))
        {
            // The engine is going away anyway.
        }
        Release(voice);
        App.Instance.TrimSoon(TimeSpan.FromSeconds(3));
    }

    void PollTick(object? sender, EventArgs e)
    {
        try
        {
            if (_voice is { } voice)
            {
                var status = Invoke(voice, "Status", BindingFlags.GetProperty);
                try
                {
                    if (Convert.ToInt32(Invoke(status!, "RunningState", BindingFlags.GetProperty)) != StateDone)
                        return;
                }
                finally
                {
                    Release(status);
                }
            }
        }
        catch (Exception ex) when (IsComFailure(ex) || ex is FormatException)
        {
            // Treat an unreadable state as finished.
        }
        Stop();
    }

    /// <summary>The first installed voice whose primary language is <paramref name="language"/>.</summary>
    static object? FindVoice(object voice, int language)
    {
        var voices = Invoke(voice, "GetVoices", BindingFlags.InvokeMethod)!;
        try
        {
            int count = Convert.ToInt32(Invoke(voices, "Count", BindingFlags.GetProperty));
            for (int i = 0; i < count; i++)
            {
                var candidate = Invoke(voices, "Item", BindingFlags.InvokeMethod, i)!;
                // "409" or "409;9": the language id of the voice in hex.
                string lang = Invoke(candidate, "GetAttribute", BindingFlags.InvokeMethod, "Language") as string ?? "";
                if (int.TryParse(lang.Split(';')[0], System.Globalization.NumberStyles.HexNumber, null, out int lcid)
                    && (lcid & 0x3FF) == language)
                    return candidate;
                Release(candidate);
            }
            return null;
        }
        finally
        {
            Release(voices);
        }
    }

    /// <summary>One COM automation call (IDispatch), with the COM error unwrapped.</summary>
    static object? Invoke(object target, string name, BindingFlags kind, params object?[] args)
    {
        try
        {
            return target.GetType().InvokeMember(name, kind, null, target, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    static bool IsComFailure(Exception ex) =>
        ex is COMException or InvalidCastException or MissingMethodException or MissingMemberException or TargetInvocationException;

    static void Release(object? com)
    {
        if (com is not null && Marshal.IsComObject(com))
            Marshal.FinalReleaseComObject(com);
    }
}
