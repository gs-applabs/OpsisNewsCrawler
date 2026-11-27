using System.Net;
using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public sealed class FeedDiscoveryDeadlineTests
{
    private sealed class Reply(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct) =>
            Task.FromResult(response(req));
    }
    private sealed class StalledStream : MemoryStream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
    }

    [Fact]
    public async Task BodyDeadlineAppliesAfterHeadersAndCallerCancellationStillPropagates()
    {
        using var handler = new Reply(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new StalledStream()) });
        using var provider = new RssNewsProvider(handler, TimeSpan.FromMilliseconds(150));
        var test = await provider.TestAsync("https://news.example.org/rss").WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(test.Ok);
        var feed = new FeedSource("Stalled", "https://news.example.org/rss");
        Assert.Empty(await provider.FetchAsync(new[] { feed }, 5).WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Contains("Stalled", provider.LastFailedFeeds);
        await provider.FetchAsync(new[] { feed }, 5);
        Assert.Contains("Stalled", provider.LastFailedFeeds);
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.TestAsync(feed.Url, cancel.Token));
    }

    [Fact]
    public async Task DiscoveryUsesRedirectedPageAndRejectsUnsafeDeclarations()
    {
        using var handler = new RedirectGuardHandler(new Reply(req =>
        {
            if (req.RequestUri!.Host == "old.example.org")
            {
                var redirect = new HttpResponseMessage(HttpStatusCode.Found);
                redirect.Headers.Location = new Uri("https://new.example.org/news/index.html");
                return redirect;
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(
                "<html><link type='application/rss+xml' href='feed.xml'><link type='application/rss+xml' href='http://new.example.org/plain'><link type='application/rss+xml' href='https://127.0.0.1/admin'><link type='application/rss+xml' href='file:///secret'></html>") };
        }));
        var found = await FeedDiscovery.DiscoverAsync("https://old.example.org/", handler: handler);
        Assert.Equal("https://new.example.org/news/feed.xml", Assert.Single(found).Url);
    }

    [Fact]
    public async Task RedirectStripsCrossAuthorityCredentialsAndOldValidators()
    {
        using var client = new HttpClient(new RedirectGuardHandler(new Reply(req =>
        {
            if (req.RequestUri!.Port == 443)
            {
                var redirect = new HttpResponseMessage(HttpStatusCode.Found);
                redirect.Headers.Location = new Uri("https://news.example.org:8443/new");
                return redirect;
            }
            Assert.False(req.Headers.Contains("Authorization"));
            Assert.False(req.Headers.Contains("Cookie"));
            Assert.False(req.Headers.Contains("If-None-Match"));
            return new HttpResponseMessage(HttpStatusCode.OK);
        })));
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://news.example.org/old");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer fixture");
        request.Headers.TryAddWithoutValidation("Cookie", "test=1");
        request.Headers.TryAddWithoutValidation("If-None-Match", "\"old\"");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
