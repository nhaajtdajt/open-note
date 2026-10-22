using System.Net;
using System.Net.Http;
using OpenNote.Core;

namespace OpenNote.Tests;

public class GeminiKeyCheckerTests
{
    sealed class Fake(Func<string, HttpStatusCode> answer) : HttpMessageHandler
    {
        public List<string> Urls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel)
        {
            Urls.Add(request.RequestUri!.ToString());
            var key = request.Headers.GetValues("x-goog-api-key").Single();
            return Task.FromResult(new HttpResponseMessage(answer(key)));
        }
    }

    sealed class Offline : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel) =>
            throw new HttpRequestException("no network");
    }

    const string K1 = "AIzaSyA-1111111111111111111111111111111";
    const string K2 = "AQ.Ab8RN6-2222222222222222222222222222222";

    [Fact]
    public void ParseKeys_AcceptsLinesCommasEnvLinesAndQuotes_AndDropsDuplicates()
    {
        var text = $"""
            {K1}
            GEMINI_API_KEY_2={K2}
            "{K1}",  short
            """;
        Assert.Equal([K1, K2], GeminiKeyChecker.ParseKeys(text));
        Assert.Empty(GeminiKeyChecker.ParseKeys("   \r\n "));
    }

    [Fact]
    public void Mask_ShowsOnlyTheEnds()
    {
        Assert.Equal("AIzaSy…1111", GeminiKeyChecker.Mask(K1));
        Assert.DoesNotContain("1111111111", GeminiKeyChecker.Mask(K1));
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, KeyStatus.Works, true)]
    [InlineData(HttpStatusCode.TooManyRequests, KeyStatus.QuotaUsedUp, true)]
    [InlineData(HttpStatusCode.BadRequest, KeyStatus.Invalid, false)]
    [InlineData(HttpStatusCode.Unauthorized, KeyStatus.Invalid, false)]
    [InlineData(HttpStatusCode.Forbidden, KeyStatus.NoAccess, false)]
    [InlineData(HttpStatusCode.ServiceUnavailable, KeyStatus.ServiceProblem, true)]
    public async Task StatusCodes_MapToWhatTheUserSees(HttpStatusCode code, KeyStatus expected, bool kept)
    {
        var result = await GeminiKeyChecker.CheckAsync(K1, new HttpClient(new Fake(_ => code)));
        Assert.Equal(expected, result.Status);
        Assert.Equal(kept, result.Keep);
    }

    [Fact]
    public async Task NoInternet_IsReported_AndTheKeyIsKept()
    {
        var result = await GeminiKeyChecker.CheckAsync(K1, new HttpClient(new Offline()));
        Assert.Equal(KeyStatus.Offline, result.Status);
        Assert.True(result.Keep);
    }

    [Fact]
    public async Task CheckAll_ChecksEveryKey_WithAFreeModelsCall()
    {
        var fake = new Fake(key => key == K2 ? HttpStatusCode.BadRequest : HttpStatusCode.OK);
        var results = await GeminiKeyChecker.CheckAllAsync([K1, K2], new HttpClient(fake));
        Assert.Equal([KeyStatus.Works, KeyStatus.Invalid], results.Select(r => r.Status));
        Assert.All(fake.Urls, u => Assert.Contains("/models?pageSize=1", u)); // listing models costs no generation quota
    }
}
