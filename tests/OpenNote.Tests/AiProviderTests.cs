using System.Net;
using System.Net.Http;
using System.Text;
using OpenNote.Core;

namespace OpenNote.Tests;

public class AiProviderTests
{
    /// <summary>Answers per provider (by host) and records every call.</summary>
    sealed class FakeServices(Func<string, HttpStatusCode> opencodeStatus, string opencodeContent) : HttpMessageHandler
    {
        public List<string> Calls { get; } = [];
        public string? LastAuth { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel)
        {
            bool opencode = request.RequestUri!.Host == "opencode.ai";
            string model = opencode ? "" : request.RequestUri.AbsolutePath.Split('/').Last();
            Calls.Add(opencode ? "opencode" : "gemini");
            if (!opencode)
                return Json(HttpStatusCode.OK, GeminiBody);

            LastAuth = request.Headers.Authorization?.ToString();
            LastBody = await request.Content!.ReadAsStringAsync(cancel);
            var status = opencodeStatus(LastBody);
            return Json(status, status == HttpStatusCode.OK ? ChatBody(opencodeContent) : "{}");
        }

        static HttpResponseMessage Json(HttpStatusCode code, string body) =>
            new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }

    const string Answer = """{"minimal":"I went.","natural":"I went there.","yourMeaning":"Tôi đã đi.","naturalMeaning":"Tôi đã đến đó.","verdict":"unnatural","comment":"Hơi cụt.","changes":"Sửa thì."}""";

    static readonly string GeminiBody = System.Text.Json.JsonSerializer.Serialize(new
    {
        candidates = new[] { new { content = new { parts = new[] { new { text = Answer } } } } },
    });

    static string ChatBody(string content) => System.Text.Json.JsonSerializer.Serialize(new
    {
        choices = new[] { new { message = new { role = "assistant", content } } },
    });

    static AiCorrector Make(FakeServices fake, string opencodeKey = "oc-key") => new(
    [
        new AiProvider("OpenCode", AiProvider.ApiStyle.OpenAiChat, AiProvider.OpenCodeUrl, string.IsNullOrEmpty(opencodeKey) ? [] : [opencodeKey], ["big-pickle", "glm"]),
        new AiProvider("Gemini", AiProvider.ApiStyle.Gemini, AiProvider.GeminiUrl, ["g1"], ["flash"]),
    ], new HttpClient(fake));

    [Fact]
    public async Task OpenCode_IsTriedFirst_WithBearerKey_AndFullAnalysisIsRead()
    {
        var fake = new FakeServices(_ => HttpStatusCode.OK, Answer);
        var c = await Make(fake).CorrectAsync("i go");

        Assert.Equal(["opencode"], fake.Calls);
        Assert.Equal("Bearer oc-key", fake.LastAuth);
        Assert.Contains("\"model\":\"big-pickle\"", fake.LastBody);
        Assert.Equal(new Correction("I went.", "I went there.", "Sửa thì.", "Tôi đã đi.", "Tôi đã đến đó.", Verdict.Unnatural, "Hơi cụt."), c);
    }

    [Fact]
    public async Task AnswerWrappedInCodeFenceOrThinkBlock_IsStillRead()
    {
        var fenced = "<think>hmm, let me see</think>\n```json\n" + Answer + "\n```";
        var c = await Make(new FakeServices(_ => HttpStatusCode.OK, fenced)).CorrectAsync("i go");
        Assert.Equal("I went.", c.Minimal);
        Assert.Equal(Verdict.Unnatural, c.Verdict);
    }

    [Fact]
    public async Task RejectedOpenCodeKey_FallsBackToGemini()
    {
        var fake = new FakeServices(_ => HttpStatusCode.Forbidden, "");
        var c = await Make(fake).CorrectAsync("i go");
        Assert.Equal("I went.", c.Minimal);
        Assert.Equal(["opencode", "opencode", "gemini"], fake.Calls);   // each OpenCode model once, then Gemini
    }

    [Fact]
    public async Task UnreadableOpenCodeAnswer_TriesNextModelThenGemini()
    {
        var fake = new FakeServices(_ => HttpStatusCode.OK, "I'm sorry, I can't help with that.");
        await Make(fake).CorrectAsync("i go");
        Assert.Equal(["opencode", "opencode", "gemini"], fake.Calls);
    }

    [Fact]
    public async Task ProviderWithoutAKey_IsSkipped()
    {
        var fake = new FakeServices(_ => HttpStatusCode.OK, Answer);
        await Make(fake, opencodeKey: "").CorrectAsync("i go");
        Assert.Equal(["gemini"], fake.Calls);
    }

    [Fact]
    public void TranslateMode_IsRecognised()
    {
        var answer = """{"mode":"translate","minimal":"I am looking forward to it.","natural":"I can't wait.","yourMeaning":"","naturalMeaning":"Tôi rất mong chờ.","verdict":"","comment":"Dùng can't wait cho tự nhiên.","changes":""}""";
        var c = AiCorrector.Parse(AiProvider.ApiStyle.OpenAiChat, ChatBody(answer));
        Assert.True(c.Translated);
        Assert.Equal(Verdict.Unknown, c.Verdict);
        Assert.Equal("I can't wait.", c.Natural);
        Assert.False(AiCorrector.Parse(AiProvider.ApiStyle.OpenAiChat, ChatBody(Answer)).Translated);
    }

    [Theory]
    [InlineData("good", Verdict.Good)]
    [InlineData("Unnatural", Verdict.Unnatural)]
    [InlineData("errors", Verdict.Errors)]
    [InlineData("wrong meaning", Verdict.WrongMeaning)]
    [InlineData("wrong_meaning", Verdict.WrongMeaning)]
    [InlineData("???", Verdict.Unknown)]
    [InlineData("", Verdict.Unknown)]
    public void VerdictWords_AreUnderstood(string word, Verdict expected) =>
        Assert.Equal(expected, AiCorrector.ParseVerdict(word));

    [Fact]
    public async Task ExtraService_FromSettings_IsUsedBeforeGemini()
    {
        var dir = Path.Combine(Path.GetTempPath(), "OpenNoteTests-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(AppSettings.PathIn(dir), """
                { "aiServices": [ { "name": "OpenRouter", "baseUrl": "https://openrouter.ai/api/v1", "apiKey": "or-key", "models": ["openrouter/free"] } ],
                  "geminiApiKeys": ["g1"] }
                """);
            var settings = AppSettings.Load(dir, out var error);
            Assert.Null(error);
            var seen = new List<(string Host, string Path, string? Auth)>();
            var handler = new Recorder(r => seen.Add((r.RequestUri!.Host, r.RequestUri.AbsolutePath, r.Headers.Authorization?.ToString())), Answer);
            var corrector = settings.CreateAiCorrector(new HttpClient(handler));

            var c = await corrector.CorrectAsync("i go");

            Assert.Equal("I went.", c.Minimal);
            var call = Assert.Single(seen);
            Assert.Equal(("openrouter.ai", "/api/v1/chat/completions", "Bearer or-key"), call);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    sealed class Recorder(Action<HttpRequestMessage> onRequest, string content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel)
        {
            onRequest(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ChatBody(content), Encoding.UTF8, "application/json") });
        }
    }

    [Fact]
    public void Settings_BuildProviders_FromKeysAndEnvironment()
    {
        var s = new AppSettings { OpenCodeApiKey = "  k  " };
        Assert.Equal(["  k  "], s.EffectiveOpenCodeKeys());
        Assert.Equal("big-pickle", s.OpenCodeModels[0]);
        Assert.True(s.CreateAiCorrector().IsConfigured);
        Assert.False(new AppSettings().CreateAiCorrector().IsConfigured || Environment.GetEnvironmentVariable("OPENCODE_API_KEY") is not null
            || Environment.GetEnvironmentVariable("GEMINI_API_KEY") is not null);
    }
}
