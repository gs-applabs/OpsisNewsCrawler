namespace OpsisNewsCrawler.Core;

/// <summary>
/// Sanitizes the user's manually configured live-video catalogue. Never fetches,
/// launches, embeds or assumes a feed's provenance or broadcast rights.
/// </summary>
public static class LiveVideoStudio
{
    public static LiveFeed Clean(LiveFeed? original)
    {
        if (original is null) return new LiveFeed();
        var name = (original.Name ?? "").Trim();
        var region = (original.Region ?? "").Trim().ToUpperInvariant();
        return new LiveFeed
        {
            Name = name.Length > 100 ? name[..100] : name,
            Url = (original.Url ?? "").Trim(),
            Enabled = original.Enabled,
            Priority = Math.Clamp(original.Priority, -100, 100),
            Region = region.Length == 2 && region.All(c => c is >= 'A' and <= 'Z') ? region : "",
            EventKeywords = (original.EventKeywords ?? new())
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Select(k => k.Trim())
                .Where(k => k.Length is >= 3 and <= 60)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(20).ToList()
        };
    }

    public static List<LiveFeed> CleanAll(IEnumerable<LiveFeed>? raw)
    {
        return (raw ?? Enumerable.Empty<LiveFeed>())
            .Where(f => f is not null && Breaking.ToPlayableUrl(f.Url) is not null)
            .DistinctBy(f => Breaking.ToPlayableUrl(f.Url), StringComparer.OrdinalIgnoreCase)
            .Take(64).Select(Clean).ToList();
    }
}
