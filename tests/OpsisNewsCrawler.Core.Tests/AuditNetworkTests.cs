using System.Net;
using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

/// <summary>Full audit (2026-11-24): redirects and IPv6-embedded IPv4 addresses must not reach the local network.</summary>
public sealed class AuditNetworkTests
{
    private sealed class Script : HttpMessageHandler
    {
        private readonly Func<Uri, HttpResponseMessage> next;
        public List<Uri> Seen { get; } = new();
        public Script(Func<Uri, HttpResponseMessage> next) => this.next = next;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { Seen.Add(request.RequestUri!); return Task.FromResult(next(request.RequestUri!)); }
    }

    private static HttpResponseMessage Redirect(string to, HttpStatusCode code = HttpStatusCode.Found)
    { var r = new HttpResponseMessage(code); r.Headers.Location = new Uri(to, UriKind.RelativeOrAbsolute); return r; }

    private static HttpClient Client(Script s) => new(new RedirectGuardHandler(s));

    [Theory]
    [InlineData("http://127.0.0.1/admin")]
    [InlineData("https://localhost/x")]
    [InlineData("https://192.168.1.1/")]
    [InlineData("https://printer/")]
    [InlineData("https://[::ffff:10.0.0.5]/")]
    [InlineData("https://[64:ff9b::a00:1]/")]
    [InlineData("https://[2002:c0a8:0101::1]/")]
    public async Task PublicFeedCannotRedirectToLocalOrPrivateAddress(string target)
    {
        var s = new Script(u => u.Host == "feeds.example.com" ? Redirect(target) : new HttpResponseMessage(HttpStatusCode.OK));
        await Assert.ThrowsAsync<HttpRequestException>(() => Client(s).GetAsync("https://feeds.example.com/rss"));
        Assert.Single(s.Seen);   // the local address was never contacted
    }

    [Fact]
    public async Task HttpsToHttpDowngradeIsRefusedButRelativeRedirectWorks()
    {
        var s = new Script(u => u.AbsolutePath == "/old" ? Redirect("/new", HttpStatusCode.MovedPermanently)
            : u.AbsolutePath == "/new" ? new HttpResponseMessage(HttpStatusCode.OK)
            : Redirect("http://feeds.example.com/plain"));
        using var ok = await Client(s).GetAsync("https://feeds.example.com/old");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        await Assert.ThrowsAsync<HttpRequestException>(() => Client(s).GetAsync("https://feeds.example.com/other"));
    }

    [Fact]
    public async Task RedirectLoopStopsAfterFiveHops()
    {
        var s = new Script(u => Redirect("https://feeds.example.com/" + Guid.NewGuid().ToString("N")));
        await Assert.ThrowsAsync<HttpRequestException>(() => Client(s).GetAsync("https://feeds.example.com/start"));
        Assert.Equal(RedirectGuardHandler.MaxRedirects + 1, s.Seen.Count);
    }

    [Fact]
    public async Task FeedConfiguredOnTheLocalNetworkMayRedirectInsideIt()
    {
        var s = new Script(u => u.AbsolutePath == "/a" ? Redirect("http://192.168.1.20/b") : new HttpResponseMessage(HttpStatusCode.OK));
        using var ok = await Client(s).GetAsync("http://192.168.1.10/a");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [Theory]
    [InlineData("::ffff:127.0.0.1", false)]
    [InlineData("64:ff9b::7f00:1", false)]
    [InlineData("2002:7f00:1::", false)]
    [InlineData("::10.1.2.3", false)]
    [InlineData("198.18.0.1", false)]
    [InlineData("2606:4700:4700::1111", true)]
    [InlineData("8.8.8.8", true)]
    public void IpAddressesAreClassifiedIncludingEmbeddedIPv4(string ip, bool isPublic) =>
        Assert.Equal(isPublic, NetGuard.IsPublicHost(ip.Contains(':') ? "[" + ip + "]" : ip));
}
