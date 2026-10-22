using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace OpenNote.Services;

/// <summary>
/// Reads text aloud with the voices installed in Windows (SAPI), fully offline. The speech
/// engine is created when you start reading and released as soon as it is done, so it costs
/// nothing the rest of the time. It is used late-bound (COM), so no extra library is needed.
/// </summary>
internal sealed class SpeechService
{
    const int SpeakAsync = 1, SpeakPurgeBeforeSpeak = 2;
    const int StateDone = 1;
    const int MaxChars = 1500;

    // Primary language ids of the languages we pick voices for.
    public const int English = 0x09, Vietnamese = 0x2A;

    dynamic? _voice;
    DispatcherTimer? _poll;

    public bool IsSpeaking => _voice is not null;

    /// <summary>
    /// Starts reading <paramref name="text"/> with a voice for <paramref name="language"/>.
    /// Returns null on success, or a short message for the user when it can't be done.
    /// </summary>
    public string? Speak(string text, int language)
    {
        Stop();
        text = text.Trim();
        if (text.Length == 0)
            return "There is nothing to read.";
        if (text.Length > MaxChars)
            text = text[..MaxChars];

        try
        {
            var type = Type.GetTypeFromProgID("SAPI.SpVoice");
            if (type is null)
                return "Windows speech isn't available on this PC.";
            dynamic voice = Activator.CreateInstance(type)!;

            var chosen = FindVoice(voice, language);
            if (chosen is null)
            {
                Release(voice);
                return language == Vietnamese
                    ? "No Vietnamese voice is installed. Add one in Settings → Time & language → Speech."
                    : "No English voice is installed on this PC.";
            }
            _voice = voice; // so Stop() releases it if speaking fails
            try
            {
                voice.Voice = chosen;
            }
            finally
            {
                Release(chosen); // the engine keeps its own reference
            }
            voice.Speak(text, SpeakAsync | SpeakPurgeBeforeSpeak);

            _poll ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _poll.Tick -= PollTick;
            _poll.Tick += PollTick;
            _poll.Start();
            return null;
        }
        catch (Exception ex) when (ex is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException or InvalidCastException)
        {
            Stop();
            return "Couldn't read this text aloud.";
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
            voice.Speak("", SpeakPurgeBeforeSpeak); // cut off whatever is being read
        }
        catch (Exception ex) when (ex is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
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
            if (_voice is { } voice && (int)voice.Status.RunningState != StateDone)
                return;
        }
        catch (Exception ex) when (ex is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        {
            // Treat an unreadable state as finished.
        }
        Stop();
    }

    /// <summary>The first installed voice whose primary language is <paramref name="language"/>.</summary>
    static object? FindVoice(dynamic voice, int language)
    {
        dynamic voices = voice.GetVoices();
        try
        {
            int count = voices.Count;
            for (int i = 0; i < count; i++)
            {
                dynamic candidate = voices.Item(i);
                // "409" or "409;9": the language id of the voice in hex.
                string lang = (string)candidate.GetAttribute("Language");
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

    static void Release(object? com)
    {
        if (com is not null && Marshal.IsComObject(com))
            Marshal.FinalReleaseComObject(com);
    }
}
