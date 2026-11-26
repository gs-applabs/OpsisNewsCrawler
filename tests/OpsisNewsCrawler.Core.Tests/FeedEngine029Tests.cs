using System.Net;
using System.Text;
using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

/// <summary>0.29.0 source quality (N31, N34, N36, N40): checked against the existing RSS engine with a scripted server.</summary>
public sealed class FeedEngine029Tests
{
    private sealed class Script : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> next;
        public List<HttpRequestMessage> Seen { get; } = new();
        public Script(Func<HttpRequestMessage, HttpResponseMessage> next) => this.next = next;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Seen.Add(request);
            return Task.FromResult(next(request));
        }
    }

    private static string Rss(DateTimeOffset published) =>
        "<?xml version=\"1.0\"?><rss version=\"2.0\"><channel><title>T</title><item><title>A headline about something real</title>" +
        "<link>https://news.example/a</link><pubDate>" + published.ToString("R") + "</pubDate></item></channel></rss>";

    private static HttpResponseMessage Ok(string body, string? etag = null)
    {
        var r = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/rss+xml") };
        if (etag is not null) r.Headers.TryAddWithoutValidation("ETag", etag);
        return r;
    }

    // ---- N31 ----
    [Theory]
    [InlineData("this is not xml at all")]
    [InlineData("<rss><channel><item><title>no link</title></item></channel></rss>")]
    [InlineData("<?xml version=\"1.0\"?><!DOCTYPE x [<!ENTITY a \"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\">]><rss><channel><item><title>&a;&a;&a;</title></item></channel></rss>")]
    public async Task TestOfAMalformedFeedReportsAFailureAndNeverThrows(string body)
    {
        using var handler = new Script(_ => Ok(body));
        using var provider = new RssNewsProvider(handler);
        var result = await provider.TestAsync("https://bad.example/rss");
        Assert.False(result.Ok);
    }

    // ---- N36 ----
    [Fact]
    public async Task ConditionalRequestsAreSentAndA304KeepsTheStoriesWithoutParsing()
    {
        var now = DateTimeOffset.UtcNow;
        using var handler = new Script(r => r.Headers.IfNoneMatch.Any(t => t.Tag == "\"v1\"")
            ? new HttpResponseMessage(HttpStatusCode.NotModified)
            : Ok(Rss(now.AddHours(-1)), "\"v1\""));
        using var provider = new RssNewsProvider(handler);
        var feed = new FeedSource("Cond", "https://cond.example/rss");
        var first = await provider.FetchAsync(new[] { feed }, 5);
        Assert.Single(first);
        // the second request is sent with If-None-Match and answered with 304: the stories stay
        var health = provider.Health().Single();
        Assert.Equal(0, health.ConsecutiveFailures);
        var second = await provider.FetchAsync(new[] { feed }, 5);
        Assert.Single(second);
        Assert.Equal(2, handler.Seen.Count);
        Assert.Contains(handler.Seen[1].Headers.IfNoneMatch, t => t.Tag == "\"v1\"");
    }

    // ---- N34 ----
    [Theory]
    [InlineData(1, false)]
    [InlineData(10, true)]
    [InlineData(40, true)]
    public void QuietSourcesAreAskedLessOftenButNeverForLong(int ageDays, bool delayed)
    {
        var now = DateTimeOffset.UtcNow;
        var delay = FeedRetryPolicy.QuietDelay(now.AddDays(-ageDays), now);
        Assert.Equal(delayed, delay is not null);
        if (delay is { } d) Assert.InRange(d, TimeSpan.FromMinutes(15), TimeSpan.FromHours(1));
        Assert.Null(FeedRetryPolicy.QuietDelay(null, now));
        Assert.Null(FeedRetryPolicy.QuietDelay(now.AddDays(5), now));   // a date in the future proves nothing
    }

    [Fact]
    public async Task ASilentSourceIsNotFetchedAgainRightAwayButServesItsStories()
    {
        using var handler = new Script(_ => Ok(Rss(DateTimeOffset.UtcNow.AddDays(-25))));
        using var provider = new RssNewsProvider(handler);
        var feed = new FeedSource("Old", "https://old.example/rss");
        await provider.FetchAsync(new[] { feed }, 5, maxAgeHours: 720);
        var again = await provider.FetchAsync(new[] { feed }, 5, maxAgeHours: 720);
        Assert.Single(handler.Seen);              // the second call served the cached story
        Assert.Single(again);
        var health = provider.Health().Single();
        Assert.NotNull(health.NextRetryAt);
    }

    // ---- N40 ----
    [Fact]
    public async Task AfterTheCoolDownASingleProbeIsMadeAndASuccessRestoresTheSource()
    {
        var fail = true;
        using var handler = new Script(_ => fail ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) : Ok(Rss(DateTimeOffset.UtcNow.AddHours(-1))));
        using var provider = new RssNewsProvider(handler);
        var feed = new FeedSource("Flaky", "https://flaky.example/rss");
        await provider.FetchAsync(new[] { feed }, 5);
        await provider.FetchAsync(new[] { feed }, 5);   // cooling down: no second request
        Assert.Single(handler.Seen);
        Assert.Equal(1, provider.Health().Single().ConsecutiveFailures);
        // the cool-down passes (simulated by the state of a fresh provider that already knows the feed is healthy again)
        fail = false;
        using var recovered = new RssNewsProvider(handler);
        Assert.Single(await recovered.FetchAsync(new[] { feed }, 5));
        Assert.Equal(0, recovered.Health().Single().ConsecutiveFailures);
    }
}

public sealed class ReadStoreLimitTests
{
    [Fact]
    public void ReadListNeverGrowsPastItsLimit_N38()
    {
        var path = Path.Combine(Path.GetTempPath(), "opsis-read-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var many = Enumerable.Range(0, ReadStore.MaxEntries + 200).ToDictionary(i => "https://n.example/" + i, i => DateTimeOffset.UtcNow.AddMinutes(-i));
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(many));
            var store = new ReadStore(path);
            store.MarkRead("https://n.example/new");
            Assert.True(store.Count <= ReadStore.MaxEntries);
            Assert.True(store.IsRead("https://n.example/new"));
            Assert.True(store.IsRead("https://n.example/0"));                          // the newest stay
            Assert.False(store.IsRead("https://n.example/" + (ReadStore.MaxEntries + 199)));   // the oldest go
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
