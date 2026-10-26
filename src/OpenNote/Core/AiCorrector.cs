using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace OpenNote.Core;

/// <summary>How the AI judges the user's original text.</summary>
public enum Verdict
{
    /// <summary>The AI didn't say.</summary>
    Unknown,
    /// <summary>Correct and natural.</summary>
    Good,
    /// <summary>Understandable and grammatical, but not how a native speaker would say it.</summary>
    Unnatural,
    /// <summary>Spelling or grammar mistakes.</summary>
    Errors,
    /// <summary>Says something other than what was probably meant, or is confusing.</summary>
    WrongMeaning,
}

/// <summary>
/// The AI's answer. For text in the language being learned (or mixed): two rewrites, what the user's own sentence actually means (in their own language),
/// what the natural version means, and a short verdict with a comment.
/// </summary>
public sealed record Correction(
    string Minimal,
    string Natural,
    string Changes,
    string YourMeaning = "",
    string NaturalMeaning = "",
    Verdict Verdict = Verdict.Unknown,
    string Comment = "",
    bool Translated = false);

public sealed class AiException(string message) : Exception(message);

/// <summary>One AI service the corrector can use.</summary>
public sealed record AiProvider(string Name, AiProvider.ApiStyle Style, string BaseUrl, IReadOnlyList<string> Keys, IReadOnlyList<string> Models)
{
    public enum ApiStyle { Gemini, OpenAiChat }

    public bool IsUsable => Keys.Count > 0 && Models.Count > 0;

    public const string GeminiUrl = "https://generativelanguage.googleapis.com/v1beta/models/";
    public const string OpenCodeUrl = "https://opencode.ai/zen/v1/";
}

/// <summary>
/// Asks AI services to check a piece of text. Providers are tried in order; inside a provider
/// the keys are used round-robin (spreading free quota), a key that is out of quota or rejected
/// is skipped for that request, and an overloaded model falls back to the next model.
/// </summary>
public sealed class AiCorrector
{
    // {N}: the user's own language, {L}: the language they're learning (Language.EnglishName).
    // The fields and the two modes are the same for every pair of languages.
    const string InstructionsTemplate = """
        You help a {N}-speaking user with short text from their sticky notes. It is usually {L} they are learning, or {N} they want in {L}.
        Answer with a JSON object with exactly these string fields:
        {MODE}

        If mode is "translate" (do NOT check the {N}; translate it into {L}):
        - minimal: a faithful {L} translation that stays close to the {N} wording.
        - natural: how a native {L} speaker would naturally say the same thing (idiomatic, same tone and meaning).
        - yourMeaning: empty string.
        - naturalMeaning: the {N} meaning of "natural", so the user can confirm it says what they meant.
        - verdict: empty string.
        - comment: 1 or 2 short {N} sentences on useful translation choices (nuance, formality, a word with several meanings). Empty string if there is nothing worth saying.
        - changes: empty string.

        If mode is "check":
        - minimal: the text with only spelling, grammar, accents/diacritics, punctuation and clearly wrong word choices fixed, as close to the original wording as possible. If nothing is wrong, the input unchanged.
        - natural: the same meaning and tone, rewritten the way a native speaker would say it. Do not add facts or change the meaning.
        - yourMeaning: what the user's ORIGINAL text really says, translated literally into {N}, so they can see what their sentence means. If the original is {N}, restate it in simple {N}.
        - naturalMeaning: the {N} meaning of "natural".
        - verdict: exactly one of "good" (correct and natural), "unnatural" (understandable and grammatical, but a native speaker would not say it this way), "errors" (spelling or grammar mistakes), "wrong_meaning" (it says something different from what the user most likely meant, or is confusing).
        - comment: 1 to 3 short {N} sentences judging the original text: is it right or wrong, is it natural, and why. If the note holds a {N} sentence together with a {L} version of it, treat the {N} as the meaning the user wants, and say whether the {L} really expresses it. If the meanings differ, {FORM}
        - changes: one short {N} sentence on what was fixed (or that it was already correct).

        Rules:
        - In "check" mode keep the language of the input in minimal and natural ({L} stays {L}, mixed stays mixed).
        - Keep line breaks, and keep list markers (☐ ☑ •) at the start of lines.
        - Keep names, numbers, links and code exactly as written.
        - The message may start with CONTEXT (the note's title and the words written just before) and then give TEXT. Use the context only to understand the topic, what the user means and which words fit; check or translate only TEXT, and never put the context in your answer. "Only {N}" above is about TEXT.
        - Output only the JSON object, with no other text.
        """;

    /// <summary>The instructions for a user whose language is <paramref name="native"/>, learning <paramref name="learning"/>.</summary>
    internal static string BuildInstructions(Language native, Language learning) => InstructionsTemplate
        .Replace("{MODE}", native == learning
            ? "- mode: always \"check\" (the user writes in the language they are learning)."
            : "- mode: \"translate\" if the text is only {N} (there is no {L} sentence in it to check), otherwise \"check\".")
        .Replace("{FORM}", native.Code == "vi"
            ? "say it in this form: \"Nếu nghĩa tiếng Việt là '…' thì nên viết '…'. Còn câu bạn viết nghĩa là '…'.\""
            : "say it in {N}, in this form: \"If you mean '…', write '…'. What you wrote means '…'.\"")
        .Replace("{N}", native.EnglishName)
        .Replace("{L}", learning.EnglishName);

    readonly string _instructions;

    /// <summary>The text to work on, with the note's context in front of it when there is any.</summary>
    internal static string UserMessage(string text, string context) =>
        string.IsNullOrWhiteSpace(context)
            ? text
            : $"CONTEXT (only to understand the topic; do not check, translate or repeat it):\n{context}\n\nTEXT:\n{text}";

    // 20 s: a model that is up answers within a few seconds; one that hangs is given up on soon
    // enough to try the next model while you're still waiting.
    static readonly Lazy<HttpClient> SharedClient = new(() => new HttpClient { Timeout = TimeSpan.FromSeconds(20) });

    readonly IReadOnlyList<AiProvider> _providers;
    readonly HttpClient? _client;
    readonly Dictionary<string, int> _nextKey = [];

    /// <param name="native">The user's own language (Vietnamese when not given).</param>
    /// <param name="learning">The language they're learning (English when not given).</param>
    public AiCorrector(IEnumerable<AiProvider> providers, HttpClient? client = null, Language? native = null, Language? learning = null)
    {
        _instructions = BuildInstructions(native ?? Language.Find("vi")!, learning ?? Language.English);
        _providers = providers
            .Select(p => p with
            {
                Keys = p.Keys.Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim()).ToList(),
                Models = p.Models.Where(m => !string.IsNullOrWhiteSpace(m)).Select(m => m.Trim()).ToList(),
            })
            .Where(p => p.IsUsable)
            .ToList();
        _client = client;
    }

    public bool IsConfigured => _providers.Count > 0;

    HttpClient Client => _client ?? SharedClient.Value; // created on first use only

    /// <param name="context">The note's title and the words before <paramref name="text"/> (see <see cref="AiContext"/>), read for meaning only.</param>
    public async Task<Correction> CorrectAsync(string text, string context = "", CancellationToken cancel = default)
    {
        text = UserMessage(text, context);
        if (!IsConfigured)
            throw new AiException(Ui.T("No AI key yet. Press Ctrl+Alt+K to add a free Gemini API key."));

        var failures = new List<string>();
        foreach (var provider in _providers)
        {
            foreach (var model in provider.Models)
            {
                for (int attempt = 0; attempt < provider.Keys.Count; attempt++)
                {
                    int keyIndex = NextKey(provider);
                    HttpRequestMessage request;
                    try
                    {
                        request = BuildRequest(provider, model, provider.Keys[keyIndex], _instructions, text);
                    }
                    catch (Exception ex) when (ex is FormatException or InvalidOperationException or ArgumentException)
                    {
                        // A key with a stray character or an address without https://. The exception's
                        // message can contain the key, so it is never shown or logged.
                        failures.Add($"{provider.Name} key {keyIndex + 1}: the key or address in settings.json isn't valid");
                        continue;
                    }

                    HttpResponseMessage response;
                    try
                    {
                        using (request)
                            response = await Client.SendAsync(request, cancel).ConfigureAwait(false);
                    }
                    catch (HttpRequestException)
                    {
                        failures.Add($"{provider.Name} {model}: no connection");
                        goto nextModel;
                    }
                    catch (TaskCanceledException) when (!cancel.IsCancellationRequested)
                    {
                        failures.Add($"{provider.Name} {model}: timeout");
                        goto nextModel;
                    }

                    using (response)
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            try
                            {
                                return Parse(provider.Style, await response.Content.ReadAsStringAsync(cancel).ConfigureAwait(false));
                            }
                            catch (AiException ex)
                            {
                                failures.Add($"{provider.Name} {model}: {ex.Message}");
                                goto nextModel; // an unreadable answer: try the next model
                            }
                        }

                        var status = response.StatusCode;
                        failures.Add($"{provider.Name} {model} key {keyIndex + 1}: {(int)status}");
                        if (status is HttpStatusCode.TooManyRequests or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                            continue; // this key is out of quota or rejected: try the next key
                        goto nextModel; // overloaded / unknown model: try the next model
                    }
                }
            nextModel:;
            }
        }
        throw new AiException(Explain(failures));
    }

    int NextKey(AiProvider provider)
    {
        lock (_nextKey) // two notes can be checking at once, on thread-pool threads
        {
            _nextKey.TryGetValue(provider.Name, out int n);
            _nextKey[provider.Name] = n + 1;
            return (int)((uint)n % (uint)provider.Keys.Count);
        }
    }

    static HttpRequestMessage BuildRequest(AiProvider provider, string model, string key, string instructions, string text)
    {
        if (provider.Style == AiProvider.ApiStyle.OpenAiChat)
        {
            var chat = new JsonObject
            {
                ["model"] = model,
                ["temperature"] = 0.2,
                ["max_tokens"] = 3000,
                ["messages"] = new JsonArray(
                    new JsonObject { ["role"] = "system", ["content"] = instructions },
                    new JsonObject { ["role"] = "user", ["content"] = text }),
            };
            var request = new HttpRequestMessage(HttpMethod.Post, provider.BaseUrl.TrimEnd('/') + "/chat/completions")
            {
                Content = new StringContent(chat.ToJsonString(), Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("Authorization", "Bearer " + key);
            return request;
        }

        var properties = new JsonObject();
        foreach (var field in new[] { "mode", "minimal", "natural", "yourMeaning", "naturalMeaning", "verdict", "comment", "changes" })
            properties[field] = new JsonObject { ["type"] = "STRING" };
        var body = new JsonObject
        {
            ["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["text"] = instructions }) },
            ["contents"] = new JsonArray(new JsonObject
            {
                ["role"] = "user",
                ["parts"] = new JsonArray(new JsonObject { ["text"] = text }),
            }),
            ["generationConfig"] = new JsonObject
            {
                ["temperature"] = 0.2,
                ["responseMimeType"] = "application/json",
                ["responseSchema"] = new JsonObject
                {
                    ["type"] = "OBJECT",
                    ["properties"] = properties,
                    ["required"] = new JsonArray("minimal", "natural", "changes"),
                },
            },
        };
        var gemini = new HttpRequestMessage(HttpMethod.Post, $"{provider.BaseUrl}{Uri.EscapeDataString(model)}:generateContent")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        gemini.Headers.Add("x-goog-api-key", key); // header, not URL, so keys never land in logs or proxies
        return gemini;
    }

    internal static Correction Parse(AiProvider.ApiStyle style, string responseJson)
    {
        try
        {
            var root = JsonNode.Parse(responseJson);
            string answer = style == AiProvider.ApiStyle.Gemini ? GeminiAnswer(root) : ChatAnswer(root);
            if (string.IsNullOrWhiteSpace(answer))
                throw new AiException("no answer for this text");

            var result = JsonNode.Parse(ExtractJsonObject(answer));
            string Field(string name) => result?[name]?.GetValue<string>()?.Trim() ?? "";
            var minimal = Field("minimal");
            var natural = Field("natural");
            if (minimal.Length == 0 && natural.Length == 0)
                throw new AiException("no suggestion for this text");
            return new Correction(
                minimal.Length > 0 ? minimal : natural,
                natural.Length > 0 ? natural : minimal,
                Field("changes"),
                Field("yourMeaning"),
                Field("naturalMeaning"),
                ParseVerdict(Field("verdict")),
                Field("comment"),
                Translated: Field("mode").Equals("translate", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            throw new AiException("unreadable answer");
        }
    }

    static string GeminiAnswer(JsonNode? root)
    {
        // No candidates happens when Gemini blocks the text (safety filters) or returns nothing.
        var candidate = root?["candidates"] is JsonArray { Count: > 0 } candidates ? candidates[0] : null;
        var parts = candidate?["content"]?["parts"] as JsonArray;
        return string.Concat(parts?
            .Where(p => p?["thought"]?.GetValue<bool>() != true)
            .Select(p => p?["text"]?.GetValue<string>()) ?? []);
    }

    static string ChatAnswer(JsonNode? root) =>
        root?["choices"] is JsonArray { Count: > 0 } choices ? choices[0]?["message"]?["content"]?.GetValue<string>() ?? "" : "";

    /// <summary>
    /// Models without a strict JSON mode may wrap the object in a code fence or add a
    /// &lt;think&gt; block; take the outermost {...}.
    /// </summary>
    internal static string ExtractJsonObject(string answer)
    {
        answer = Regex.Replace(answer, @"<think>.*?</think>", "", RegexOptions.Singleline);
        int start = answer.IndexOf('{'), end = answer.LastIndexOf('}');
        return start >= 0 && end > start ? answer[start..(end + 1)] : answer;
    }

    internal static Verdict ParseVerdict(string text) => text.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_') switch
    {
        "good" or "correct" => Verdict.Good,
        "unnatural" => Verdict.Unnatural,
        "errors" or "error" => Verdict.Errors,
        "wrong_meaning" or "wrongmeaning" => Verdict.WrongMeaning,
        _ => Verdict.Unknown,
    };

    static string Explain(List<string> failures)
    {
        bool quota = failures.All(f => f.EndsWith(": 429"));
        bool rejected = failures.All(f => f.EndsWith(": 401") || f.EndsWith(": 403"));
        bool offline = failures.All(f => f.EndsWith("no connection"));
        string summary = offline ? Ui.T("Couldn't reach the AI service. Check your internet connection.")
            : quota ? Ui.T("All API keys are out of quota for now. Try again later.")
            : rejected ? Ui.T("The AI service rejected the API keys. Check them in settings.json.")
            : Ui.T("The AI service is busy right now. Try again in a moment.");
        return $"{summary}\n({string.Join(", ", failures)})";
    }
}
