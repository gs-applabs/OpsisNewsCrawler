using System.Net;
using System.Text.RegularExpressions;

namespace OpsisNewsCrawler.Core;

/// <summary>
/// Finds the RSS/Atom feed of a website from its address: either the address already is a feed, or the page declares it with
/// <c>&lt;link rel="alternate" type="application/rss+xml"&gt;</c>, or one of the usual paths (/feed, /rss.xml, ...) answers with a feed.
/// Never throws for network problems; an empty result means "nothing found".
/// </summary>
public static class FeedDiscovery
{
    private static readonly string[] CommonPaths = { "/feed", "/feed/", "/rss", "/rss.xml", "/feed.xml", "/atom.xml", "/index.xml", "/rss/all" };

    private static readonly Regex LinkTag = new(@"<link\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TypeAttr = new(@"type\s*=\s*[""']([^""']*)[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex HrefAttr = new(@"href\s*=\s*[""']([^""']*)[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TitleAttr = new(@"title\s*=\s*[""']([^""']*)[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PageTitle = new(@"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    public static async Task<IReadOnlyList<FeedSource>> DiscoverAsync(string input, CancellationToken ct = default, HttpMessageHandler? handler = null)
    {
        var result = new List<FeedSource>();
        if (!TryNormalize(input, out var site)) return result;

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(TimeSpan.FromSeconds(30));
        using var http = new HttpClient(handler ?? new RedirectGuardHandler()) { Timeout = TimeSpan.FromSeconds(10), MaxResponseContentBufferSize = 4 * 1024 * 1024 };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; OpsisNewsCrawler)");

        try
        {
            var (body, effective) = await GetTextAsync(http, site, budget.Token).ConfigureAwait(false);
            if (body is null) return result;

            var baseUri = effective ?? site;
            if (!IsAllowedDiscoveredUrl(site, baseUri)) return result;
            // The address itself is a feed.
            if (LooksLikeFeed(body))
            {
                result.Add(new FeedSource(FeedTitle(body) ?? baseUri.Host, baseUri.AbsoluteUri));
                return result;
            }

            var siteName = PageTitle.Match(body) is { Success: true } t ? Clean(t.Groups[1].Value) : site.Host;

            foreach (Match tag in LinkTag.Matches(body))
            {
                var type = TypeAttr.Match(tag.Value);
                var href = HrefAttr.Match(tag.Value);
                if (!type.Success || !href.Success) continue;
                var mime = type.Groups[1].Value.ToLowerInvariant();
                if (!mime.Contains("rss") && !mime.Contains("atom")) continue;
                if (!Uri.TryCreate(baseUri, WebUtility.HtmlDecode(href.Groups[1].Value), out var abs)) continue;
                if (!IsAllowedDiscoveredUrl(site, abs) || !IsAllowedDiscoveredUrl(baseUri, abs)) continue;
                var title = TitleAttr.Match(tag.Value) is { Success: true } ta ? Clean(ta.Groups[1].Value) : siteName;
                if (result.All(r => r.Url != abs.AbsoluteUri)) result.Add(new FeedSource(title, abs.AbsoluteUri));
            }
            if (result.Count > 0) return result;

            // Nothing declared: try the usual places.
            foreach (var path in CommonPaths)
            {
                if (!Uri.TryCreate(new Uri(baseUri.GetLeftPart(UriPartial.Authority)), path, out var probe)) continue;
                budget.Token.ThrowIfCancellationRequested();
                var (text, final) = await GetTextAsync(http, probe, budget.Token).ConfigureAwait(false);
                if (text is not null && LooksLikeFeed(text) && IsAllowedDiscoveredUrl(site, final ?? probe) && IsAllowedDiscoveredUrl(baseUri, final ?? probe))
                {
                    result.Add(new FeedSource(FeedTitle(text) ?? siteName, (final ?? probe).AbsoluteUri));
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException or UriFormatException)
        {
        }
        return result;
    }

    private static bool TryNormalize(string input, out Uri uri)
    {
        uri = null!;
        var text = (input ?? "").Trim();
        if (text.Length == 0) return false;
        if (!text.Contains("://")) text = "https://" + text;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var parsed) || parsed.Scheme is not ("http" or "https")) return false;
        uri = parsed;
        return true;
    }

    public static bool IsAllowedDiscoveredUrl(Uri origin, Uri target) =>
        target.IsAbsoluteUri && target.Scheme is "http" or "https" && string.IsNullOrEmpty(target.UserInfo) &&
        !(origin.Scheme == "https" && target.Scheme == "http") &&
        (!NetGuard.IsPublicHost(origin.Host) || NetGuard.IsPublicHost(target.Host));

    private static async Task<(string? Body, Uri? EffectiveUri)> GetTextAsync(HttpClient http, Uri uri, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return (null, null);
            var bytes = new byte[400_000];
            await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);
            var read = 0;
            int n;
            while (read < bytes.Length && (n = await stream.ReadAsync(bytes.AsMemory(read), deadline.Token).ConfigureAwait(false)) > 0) read += n;
            return (System.Text.Encoding.UTF8.GetString(bytes, 0, read), response.RequestMessage?.RequestUri ?? uri);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException)
        {
            return (null, null);
        }
    }

    private static bool LooksLikeFeed(string text)
    {
        var head = text.Length > 2000 ? text[..2000] : text;
        return head.Contains("<rss", StringComparison.OrdinalIgnoreCase) ||
               head.Contains("<feed", StringComparison.OrdinalIgnoreCase) ||
               head.Contains("<rdf:RDF", StringComparison.OrdinalIgnoreCase);
    }

    private static string? FeedTitle(string text)
    {
        var m = PageTitle.Match(text);
        return m.Success ? Clean(m.Groups[1].Value) : null;
    }

    private static string Clean(string value)
    {
        value = Regex.Replace(WebUtility.HtmlDecode(value), @"<!\[CDATA\[|\]\]>", "");
        return Regex.Replace(value, @"\s+", " ").Trim();
    }
}
