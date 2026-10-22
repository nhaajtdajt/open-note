using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace OpenNote.Core;

public enum KeyStatus
{
    /// <summary>The key reached the Gemini API.</summary>
    Works,
    /// <summary>Valid, but its free quota is used up for now (it will work again later).</summary>
    QuotaUsedUp,
    /// <summary>Google says the key is wrong.</summary>
    Invalid,
    /// <summary>The key exists but isn't allowed to use the Gemini API (API disabled, restrictions...).</summary>
    NoAccess,
    /// <summary>No internet, or Google didn't answer in time.</summary>
    Offline,
    /// <summary>Google had a problem; the key may be fine.</summary>
    ServiceProblem,
}

public sealed record KeyCheckResult(string Key, KeyStatus Status)
{
    /// <summary>Keys worth keeping: everything except ones Google rejected.</summary>
    public bool Keep => Status is not (KeyStatus.Invalid or KeyStatus.NoAccess);

    public string Message => Status switch
    {
        KeyStatus.Works => "Works",
        KeyStatus.QuotaUsedUp => "Works, but its free quota is used up for now",
        KeyStatus.Invalid => "Not a valid key",
        KeyStatus.NoAccess => "Not allowed to use the Gemini API",
        KeyStatus.Offline => "Couldn't reach Google (no internet?)",
        _ => "Google had a problem; try again later",
    };
}

/// <summary>Reads keys pasted by the user and checks each one with a real (free) call to the Gemini API.</summary>
public static partial class GeminiKeyChecker
{
    const string ModelsUrl = "https://generativelanguage.googleapis.com/v1beta/models?pageSize=1";

    [GeneratedRegex(@"[\s,;]+")]
    private static partial Regex Separators();

    /// <summary>
    /// The keys in pasted text: one per line (or separated by spaces/commas), quotes removed,
    /// "NAME=key" lines (as in a .env file) reduced to the key, duplicates dropped.
    /// </summary>
    public static List<string> ParseKeys(string text)
    {
        var keys = new List<string>();
        foreach (var raw in Separators().Split(text))
        {
            var token = raw.Trim().Trim('"', '\'');
            int eq = token.IndexOf('=');
            if (eq >= 0)
                token = token[(eq + 1)..].Trim().Trim('"', '\'');
            if (token.Length >= 20 && !keys.Contains(token))
                keys.Add(token);
        }
        return keys;
    }

    /// <summary>Shows enough of a key to recognise it without displaying it in full.</summary>
    public static string Mask(string key) => key.Length <= 12 ? "••••••" : $"{key[..6]}…{key[^4..]}";

    /// <summary>
    /// Asks the Gemini API to list one model. It proves the key works without using any of its
    /// generation quota.
    /// </summary>
    public static async Task<KeyCheckResult> CheckAsync(string key, HttpClient client, CancellationToken cancel = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, ModelsUrl);
            request.Headers.Add("x-goog-api-key", key);
            using var response = await client.SendAsync(request, cancel).ConfigureAwait(false);
            var status = response.StatusCode switch
            {
                HttpStatusCode.OK => KeyStatus.Works,
                HttpStatusCode.TooManyRequests => KeyStatus.QuotaUsedUp,
                HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized => KeyStatus.Invalid,
                HttpStatusCode.Forbidden => KeyStatus.NoAccess,
                _ => KeyStatus.ServiceProblem,
            };
            return new KeyCheckResult(key, status);
        }
        catch (HttpRequestException)
        {
            return new KeyCheckResult(key, KeyStatus.Offline);
        }
        catch (TaskCanceledException) when (!cancel.IsCancellationRequested)
        {
            return new KeyCheckResult(key, KeyStatus.Offline);
        }
    }

    /// <summary>Checks all keys at the same time (a small wait for the user, not one per key).</summary>
    public static Task<KeyCheckResult[]> CheckAllAsync(IEnumerable<string> keys, HttpClient client, CancellationToken cancel = default) =>
        Task.WhenAll(keys.Select(k => CheckAsync(k, client, cancel)));
}
