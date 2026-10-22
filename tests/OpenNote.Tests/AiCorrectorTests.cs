using System.Net;
using System.Net.Http;
using System.Text;
using OpenNote.Core;

namespace OpenNote.Tests;

public class AiCorrectorTests
{
    /// <summary>Fake Gemini: records which key/model each request used and answers from a script.</summary>
    sealed class FakeGemini(Func<string, string, HttpStatusCode> status) : HttpMessageHandler
    {
        public List<(string Key, string Model)> Calls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel)
        {
            var key = request.Headers.GetValues("x-goog-api-key").Single();
            var model = request.RequestUri!.AbsolutePath.Split('/').Last().Split(':')[0];
            Calls.Add((key, model));
            var code = status(key, model);
            var body = code == HttpStatusCode.OK ? SuccessBody : """{"error":{"code":1}}""";
            return Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    const string SuccessBody = """
        {"candidates":[{"content":{"parts":[{"text":"{\"minimal\":\"I went to school.\",\"natural\":\"I went to school.\",\"changes\":\"Sửa thì quá khứ.\"}"}]}}]}
        """;

    static AiCorrector Make(FakeGemini fake, params string[] models) =>
        new([new AiProvider("Gemini", AiProvider.ApiStyle.Gemini, AiProvider.GeminiUrl, ["k1", "k2", "k3", "k4"], models.Length > 0 ? models : ["main", "lite"])],
            new HttpClient(fake));

    [Fact]
    public async Task RotatesKeysAcrossRequests()
    {
        var fake = new FakeGemini((_, _) => HttpStatusCode.OK);
        var ai = Make(fake);
        for (int i = 0; i < 5; i++)
            await ai.CorrectAsync("i goed to school");
        Assert.Equal(["k1", "k2", "k3", "k4", "k1"], fake.Calls.Select(c => c.Key));
    }

    [Fact]
    public async Task OutOfQuotaKey_IsSkipped()
    {
        var fake = new FakeGemini((key, _) => key == "k1" ? HttpStatusCode.TooManyRequests : HttpStatusCode.OK);
        var result = await Make(fake).CorrectAsync("i goed to school");
        Assert.Equal("I went to school.", result.Minimal);
        Assert.Equal("Sửa thì quá khứ.", result.Changes);
        Assert.Equal(["k1", "k2"], fake.Calls.Select(c => c.Key));
    }

    [Fact]
    public async Task OverloadedModel_FallsBackToNextModel()
    {
        var fake = new FakeGemini((_, model) => model == "main" ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK);
        await Make(fake).CorrectAsync("text");
        Assert.Equal(["main", "lite"], fake.Calls.Select(c => c.Model));
    }

    [Fact]
    public async Task AllKeysOutOfQuota_GivesAClearError()
    {
        var fake = new FakeGemini((_, _) => HttpStatusCode.TooManyRequests);
        var ex = await Assert.ThrowsAsync<AiException>(() => Make(fake, "main").CorrectAsync("text"));
        Assert.StartsWith("All API keys are out of quota", ex.Message);
        Assert.Equal(4, fake.Calls.Count); // each key tried once, no endless retry
    }

    [Fact]
    public async Task AMalformedKey_IsSkipped_WithoutShowingIt()
    {
        var fake = new FakeGemini((_, _) => HttpStatusCode.OK);
        const string badKey = "bad\"kéy\nsecret";
        var ai = new AiCorrector(
            [new AiProvider("Gemini", AiProvider.ApiStyle.Gemini, AiProvider.GeminiUrl, [badKey, "k2"], ["main"])], new HttpClient(fake));

        await ai.CorrectAsync("text"); // the good key still answers
        Assert.Equal(["k2"], fake.Calls.Select(c => c.Key));

        var onlyBad = new AiCorrector(
            [new AiProvider("Gemini", AiProvider.ApiStyle.Gemini, AiProvider.GeminiUrl, [badKey], ["main"])], new HttpClient(fake));
        var ex = await Assert.ThrowsAsync<AiException>(() => onlyBad.CorrectAsync("text"));
        Assert.DoesNotContain("secret", ex.Message);
    }

    [Fact]
    public async Task NoKeys_ExplainsHowToAddThem()
    {
        var ai = new AiCorrector([new AiProvider("Gemini", AiProvider.ApiStyle.Gemini, AiProvider.GeminiUrl, [], ["main"])]);
        var ex = await Assert.ThrowsAsync<AiException>(() => ai.CorrectAsync("text"));
        Assert.Contains("Ctrl+Alt+K", ex.Message);
    }

    [Fact]
    public void Parse_SkipsThoughtParts_AndRejectsEmptyAnswers()
    {
        var withThought = """
            {"candidates":[{"content":{"parts":[{"text":"thinking...","thought":true},{"text":"{\"minimal\":\"a\",\"natural\":\"b\",\"changes\":\"c\"}"}]}}]}
            """;
        const AiProvider.ApiStyle gemini = AiProvider.ApiStyle.Gemini;
        Assert.Equal(new Correction("a", "b", "c"), AiCorrector.Parse(gemini, withThought));
        Assert.Throws<AiException>(() => AiCorrector.Parse(gemini, """{"candidates":[]}"""));
        Assert.Throws<AiException>(() => AiCorrector.Parse(gemini, "not json"));
    }

    [Fact]
    public void Settings_ReadKeysAndModels()
    {
        var dir = Path.Combine(Path.GetTempPath(), "OpenNoteTests-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(AppSettings.PathIn(dir), """{ "geminiApiKeys": ["a", "b"] }""");
            var s = AppSettings.Load(dir, out var error);
            Assert.Null(error);
            Assert.Equal(["a", "b"], s.EffectiveGeminiKeys());
            Assert.Equal(new AppSettings().GeminiModels, s.GeminiModels);
            Assert.Equal("gemini-flash-latest", s.GeminiModels[0]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
