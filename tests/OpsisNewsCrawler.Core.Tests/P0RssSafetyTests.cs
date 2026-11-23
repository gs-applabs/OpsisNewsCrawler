using System.Net;
using System.Text;
using System.Xml;
using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public sealed class P0RssSafetyTests
{
    private const string Rss = "<rss><channel><item><title>News item</title><link>https://example.org/story</link></item></channel></rss>";

    private sealed class CountingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> reply;
        private readonly int pause;
        private int active;
        private int peak;
        private int calls;

        public CountingHandler(Func<HttpRequestMessage, HttpResponseMessage>? reply = null, int pause = 80)
        {
            this.reply = reply ?? (_ => new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent(Rss) });
            this.pause = pause;
        }
        public int Peak => Volatile.Read(ref peak);
        public int Calls => Volatile.Read(ref calls);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            Interlocked.Increment(ref calls);
            var activeNow = Interlocked.Increment(ref active);
            int previous;
            while ((previous = Volatile.Read(ref peak)) < activeNow &&
                   Interlocked.CompareExchange(ref peak, activeNow, previous) != previous) { }
            try
            {
                await Task.Delay(pause, ct);
                return reply(req);
            }
            finally { Interlocked.Decrement(ref active); }
        }
    }

    [Fact]
    public async Task ParallelFeedsAreCappedGloballyAndPerDomain()
    {
        using var handler = new CountingHandler();
        using var rss = new RssNewsProvider(handler);
        var feeds = Enumerable.Range(0, 15)
            .Select(i => new FeedSource("Provider " + i, $"https://domain{i % 5}.example.org/rss-{i}"));
        await rss.FetchAsync(feeds, 2);
        Assert.InRange(handler.Peak, 1, RssNewsProvider.MaxConcurrentFeeds);
        Assert.InRange(rss.PeakConcurrentRequests, 1, RssNewsProvider.MaxConcurrentFeeds);

        using var sameHandler = new CountingHandler();
        using var sameProvider = new RssNewsProvider(sameHandler);
        await sameProvider.FetchAsync(Enumerable.Range(0, 12)
            .Select(i => new FeedSource("One" + i, $"https://same.example.org/feed-{i}")), 2);
        Assert.InRange(sameHandler.Peak, 1, RssNewsProvider.MaxConcurrentPerHost);
        Assert.Equal(12, sameHandler.Calls);
    }

    [Fact]
    public async Task StreamingCapWorksWithoutContentLength()
    {
        using var good = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        Assert.Equal(new byte[] { 1, 2, 3, 4 },
            await RssNewsProvider.ReadBoundedAsync(good, 4));
        using var oversized = new MemoryStream(new byte[100]);
        await Assert.ThrowsAsync<InvalidDataException>(() => RssNewsProvider.ReadBoundedAsync(oversized, 99));
    }

    [Fact]
    public async Task OversizedFeedIsReportedAsFailedAndDoesNotEscapeOtherFeeds()
    {
        using var handler = new CountingHandler(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new MemoryStream(
                req.RequestUri!.AbsolutePath.Contains("large") ? new byte[5 * 1024 * 1024 + 1] : Encoding.UTF8.GetBytes(Rss)))
        }, pause: 1);
        using var provider = new RssNewsProvider(handler);
        var result = await provider.FetchAsync(new[]
        {
            new FeedSource("Huge", "https://safe.example.org/large"),
            new FeedSource("Normal", "https://safe.example.org/normal")
        }, 5);
        Assert.Single(result);
        Assert.Contains("Huge", provider.LastFailedFeeds);
        var failed = Assert.Single(provider.Health().Where(f => f.Url.EndsWith("/large")));
        Assert.Equal(1, failed.ConsecutiveFailures);
        Assert.NotNull(failed.NextRetryAt);
    }

    [Fact]
    public async Task RetryAfterHonouredAndCoolingDownSkipsImmediateRefetch()
    {
        using var handler = new CountingHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromMinutes(3));
            return response;
        }, pause: 1);
        using var provider = new RssNewsProvider(handler);
        var feed = new FeedSource("Throttled", "https://rss.example.org");
        await provider.FetchAsync(new[] { feed }, 2);
        await provider.FetchAsync(new[] { feed }, 2);
        Assert.Equal(1, handler.Calls);
        var health = Assert.Single(provider.Health());
        Assert.True(health.NextRetryAt > DateTimeOffset.UtcNow.AddMinutes(2));
    }

    [Theory]
    [InlineData(1, 30)]
    [InlineData(2, 60)]
    [InlineData(3, 120)]
    [InlineData(10, 1800)]
    public void ExponentialBackoffIsBounded(int failures, int seconds) =>
        Assert.Equal(TimeSpan.FromSeconds(seconds), FeedRetryPolicy.Delay(failures));

    [Fact]
    public void ParserRejectsOversizedPayloadAndRandomMalformedXmlSafely()
    {
        Assert.Throws<InvalidDataException>(() => RssNewsProvider.Parse(new byte[5 * 1024 * 1024 + 1], "Bad", 1));
        var rng = new Random(420);
        for (int i = 0; i < 120; i++)
        {
            var bytes = new byte[rng.Next(0, 4000)];
            rng.NextBytes(bytes);
            try
            {
                var parsed = RssNewsProvider.Parse(bytes, "Random", 3);
                Assert.InRange(parsed.Count, 0, 3);
            }
            catch (XmlException) { /* malformed input is correctly rejected */ }
        }
    }
}
