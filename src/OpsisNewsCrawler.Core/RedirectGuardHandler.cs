using System.Net;

namespace OpsisNewsCrawler.Core;

/// <summary>
/// Follows redirects itself so that a public feed can never bounce the program onto the user's own machine or local network
/// (loopback, private ranges, single-label names) or from HTTPS down to plain HTTP. A feed that the user configured on a
/// local address may still redirect inside the local network. At most five hops; only GET/HEAD are followed.
/// </summary>
public sealed class RedirectGuardHandler : DelegatingHandler
{
    public const int MaxRedirects = 5;

    public RedirectGuardHandler() : base(new SocketsHttpHandler { AllowAutoRedirect = false }) { }

    /// <summary>For tests: the handler underneath must NOT follow redirects itself.</summary>
    public RedirectGuardHandler(HttpMessageHandler inner) : base(inner) { }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var origin = request.RequestUri ?? throw new InvalidOperationException("request without an address");
        var originIsPublic = NetGuard.IsPublicHost(origin.Host);
        var hops = 0;
        while (true)
        {
            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode is not (HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther
                    or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
                || response.Headers.Location is null)
                return response;

            var current = request.RequestUri!;
            var next = response.Headers.Location.IsAbsoluteUri ? response.Headers.Location : new Uri(current, response.Headers.Location);
            response.Dispose();

            if (++hops > MaxRedirects) throw new HttpRequestException("too many redirects");
            if (next.Scheme != Uri.UriSchemeHttp && next.Scheme != Uri.UriSchemeHttps)
                throw new HttpRequestException("redirect to an unsupported scheme");
            if (current.Scheme == Uri.UriSchemeHttps && next.Scheme == Uri.UriSchemeHttp)
                throw new HttpRequestException("redirect from https to http refused");
            if (originIsPublic && !NetGuard.IsPublicHost(next.Host))
                throw new HttpRequestException("redirect to a local or private address refused");
            if (request.Method != HttpMethod.Get && request.Method != HttpMethod.Head)
                throw new HttpRequestException("redirect of a non-GET request refused");

            var follow = new HttpRequestMessage(request.Method, next);
            foreach (var h in request.Headers)
            {
                // Conditional headers belong to the first address only.
                if (h.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase) && !string.Equals(current.Host, next.Host, StringComparison.OrdinalIgnoreCase)) continue;
                follow.Headers.TryAddWithoutValidation(h.Key, h.Value);
            }
            request.Dispose();
            request = follow;
        }
    }
}
