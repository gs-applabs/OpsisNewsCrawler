namespace OpsisNewsCrawler.Core;

/// <summary>
/// Approximate editorial ownership grouping. Two domains known to represent the same newsroom
/// cannot be counted as independent confirmations. Unknown domains stay separate.
/// This is *not* a proof of editorial independence or fact checking.
/// </summary>
public static class PublisherIdentity
{
    private static readonly Dictionary<string, string> SharedEditorialBrands = new(StringComparer.OrdinalIgnoreCase)
    {
        ["bbc.com"] = "bbc",
        ["bbc.co.uk"] = "bbc",
        ["bbc.co"] = "bbc",
        ["ert.gr"] = "ert",
        ["ertnews.gr"] = "ert",
        ["dw.com"] = "dw",
        ["dw.de"] = "dw",
        ["apnews.com"] = "associated-press",
        ["ap.org"] = "associated-press",
        ["reuters.com"] = "reuters",
        ["reutersagency.com"] = "reuters",
        ["theguardian.com"] = "guardian",
        ["guardian.co.uk"] = "guardian",
        ["afp.com"] = "afp",
        ["afpforum.com"] = "afp"
    };

    public static string Canonical(string? publisher)
    {
        if (string.IsNullOrWhiteSpace(publisher)) return "";
        var host = publisher.Trim().TrimEnd('.').ToLowerInvariant();
        if (host.StartsWith("www.")) host = host[4..];
        if (host.StartsWith("m.")) host = host[2..];
        return SharedEditorialBrands.TryGetValue(host, out var family) ? family : host;
    }
}
