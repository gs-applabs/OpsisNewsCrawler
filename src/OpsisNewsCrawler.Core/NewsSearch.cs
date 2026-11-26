namespace OpsisNewsCrawler.Core;

/// <summary>What to look for in the news the program already holds (N10). Every empty field is ignored.</summary>
public sealed record NewsQuery(string? Text = null, DateTimeOffset? From = null, DateTimeOffset? To = null, string? Source = null, TopicWatch? Topic = null);

/// <summary>
/// N10: search over the headlines and summaries in memory (the cached news of the current run). It does not fetch anything and keeps no index of its own;
/// the offline full-text index of N39 would replace the scan below with the same query type.
/// </summary>
public static class NewsSearch
{
    public const int MaxResults = 300;

    public static IReadOnlyList<NewsItem> Find(IEnumerable<NewsItem> news, NewsQuery query)
    {
        ArgumentNullException.ThrowIfNull(news);
        ArgumentNullException.ThrowIfNull(query);
        var terms = Terms(query.Text);
        var scored = new List<(NewsItem Item, int Score)>();
        foreach (var item in news.Where(n => n.Kind == NewsItemKind.News).Take(5000))
        {
            if (query.From is { } from && item.PublishedAt < from) continue;
            if (query.To is { } to && item.PublishedAt > to) continue;
            if (!string.IsNullOrWhiteSpace(query.Source) && !string.Equals(item.Source, query.Source, StringComparison.OrdinalIgnoreCase)) continue;
            var score = 0;
            if (terms.Length > 0)
            {
                var title = Breaking.Normalize(item.Title).ToLowerInvariant();
                var body = Breaking.Normalize(item.Summary ?? "").ToLowerInvariant();
                var ok = true;
                foreach (var t in terms)
                {
                    var inTitle = title.Contains(t, StringComparison.Ordinal);
                    if (!inTitle && !body.Contains(t, StringComparison.Ordinal)) { ok = false; break; }
                    score += inTitle ? 3 : 1;
                }
                if (!ok) continue;
            }
            scored.Add((item, score));
        }
        IEnumerable<NewsItem> ranked = scored.OrderByDescending(x => x.Score).ThenByDescending(x => x.Item.PublishedAt).Select(x => x.Item);
        if (query.Topic is { } topic)
        {
            var matched = TopicMatcher.Find(ranked.ToList(), new[] { topic }).Select(m => m.Item).ToHashSet();
            ranked = ranked.Where(matched.Contains);
        }
        return ranked.Take(MaxResults).ToArray();
    }

    private static string[] Terms(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? Array.Empty<string>()
            : Breaking.Normalize(text.Length > 200 ? text[..200] : text).ToLowerInvariant()
                .Split(new[] { ' ', '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Where(t => t.Length >= 2).Distinct().Take(8).ToArray();
}
