namespace OpsisNewsCrawler.Core;

/// <summary>User-supplied stream; the app never searches for unofficial rebroadcasts or assumes rights/verification.</summary>
public sealed record LiveVideoChoice(string Name, string Url, int Priority, string Region, IReadOnlyList<string> Keywords);

/// <summary>Ranks only streams already configured by the user; deterministic and entirely offline.</summary>
public static class LiveVideoPolicy
{
    public static IReadOnlyList<LiveVideoChoice> Select(IEnumerable<LiveFeed> feeds, string? breakingHeadline,
        string? region = null)
    {
        var context = Breaking.Normalize(breakingHeadline ?? "");
        var regionKey = (region ?? "").Trim().ToUpperInvariant();
        return feeds.Where(f => f.Enabled && Breaking.ToPlayableUrl(f.Url) is not null)
            .Select(f => new LiveVideoChoice(
                string.IsNullOrWhiteSpace(f.Name) ? new Uri(Breaking.ToPlayableUrl(f.Url)!).Host : f.Name,
                Breaking.ToPlayableUrl(f.Url)!, Math.Clamp(f.Priority, -100, 100),
                (f.Region ?? "").Trim().ToUpperInvariant(),
                (f.EventKeywords ?? new()).Where(k => !string.IsNullOrWhiteSpace(k)).Take(20).ToArray()))
            .DistinctBy(x => x.Url, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(x => Score(x, context, regionKey))
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Take(32).ToList();
    }

    public static int Score(LiveVideoChoice feed, string normalizedTitle, string region)
    {
        var match = feed.Keywords.Count(w => Breaking.Normalize(w).Trim() is { Length: >= 3 } k &&
            normalizedTitle.Contains(k, StringComparison.Ordinal));
        var regionMatch = region.Length > 0 && region.Equals(feed.Region, StringComparison.OrdinalIgnoreCase);
        return feed.Priority + Math.Min(match, 10) * 20 + (regionMatch ? 15 : 0);
    }
}
