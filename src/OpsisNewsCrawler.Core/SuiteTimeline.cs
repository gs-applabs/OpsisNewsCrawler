namespace OpsisNewsCrawler.Core;

/// <summary>Local merged view. Historic events use their actual year label, not today's publication date.</summary>
public sealed record TimelineEvent(string Key, string Title, string Kind, DateTimeOffset At,
    int? HistoricalYear, string Origin, string? Url);

public sealed record TimelineLine(string Key, string Title, string Category, DateTimeOffset At,
    int? HistoricalYear, string Source, string? Url);

public static class SuiteTimeline
{
    public static IReadOnlyList<TimelineLine> Build(IEnumerable<NewsItem> news,
        IEnumerable<TimelineEvent> events, int take = 200)
    {
        var n = news.Take(500).Select(x => new TimelineLine(x.Url, x.Title, x.Kind.ToString(),
            x.PublishedAt, x.PluginMetadata?.HistoricalYear, x.Source, x.Url));
        var e = events.Take(500).Select(x => new TimelineLine(x.Key, x.Title, x.Kind.ToString(),
            x.At, x.HistoricalYear, x.Origin, x.Url));
        return n.Concat(e).Where(x => !string.IsNullOrWhiteSpace(x.Title))
            .DistinctBy(x => (x.Category, x.Key, x.Title))
            .OrderByDescending(x => x.At).Take(Math.Clamp(take,1,500)).ToArray();
    }
}
