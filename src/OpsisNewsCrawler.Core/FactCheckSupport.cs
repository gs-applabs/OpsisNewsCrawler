namespace OpsisNewsCrawler.Core;

/// <summary>
/// Editorial cross-source evidence only. Never labels a story as true/false or fabricates citations.
/// Distinct source groups and explicit contradictions are review hints, not proof.
/// </summary>
public sealed record ClaimEvidence(string Headline, IReadOnlyList<NewsItem> Reports,
    int PublisherGroups, IReadOnlyList<string> Links, string Caution);

public static class FactCheckSupport
{
    public static ClaimEvidence Review(StoryCluster cluster)
    {
        // Do not surface local-network, loopback or malformed URLs in editorial evidence.
        // The source comparison is an outbound-link viewer, never a URL fetcher.
        var real=cluster.Reports.Where(x=>NetGuard.IsPublicHttpUrl(x.Url)).Take(50).ToArray();
        return new ClaimEvidence(cluster.Headline,real,cluster.PublisherGroups,
            real.Select(x=>x.Url).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            "Multiple headlines are not independent verification. Check original documents, timestamps and retractions.");
    }

    /// <summary>Marks articles containing obvious language of corrections; does not certify them.</summary>
    public static bool MentionsRetraction(NewsItem item)
    {
        var text=Breaking.Normalize(item.Title+" "+(item.Summary??"")).ToLowerInvariant();
        return new[]{"correction","retraction","withdrawn","διόρθωση","ανάκληση","διορθωση"}
            .Any(x=>text.Contains(x,StringComparison.Ordinal));
    }
}
