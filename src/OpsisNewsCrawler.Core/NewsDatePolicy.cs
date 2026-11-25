namespace OpsisNewsCrawler.Core;

/// <summary>Normalizes RSS timestamps without inventing publication dates or replacing ancient archive dates.</summary>
public static class NewsDatePolicy
{
    public static DateTimeOffset? Normalize(DateTimeOffset? input, DateTimeOffset now)
    {
        if (input is null) return null;
        var utc = input.Value.ToUniversalTime();
        // RSS feeds frequently publish a few minutes ahead due to timezone/clock drift.
        // Such items should be treated as "now", rather than becoming perpetual breaking news.
        if (utc > now.AddMinutes(10) || utc.Year < 1970) return null;
        return utc > now ? now.ToUniversalTime() : utc;
    }
}
