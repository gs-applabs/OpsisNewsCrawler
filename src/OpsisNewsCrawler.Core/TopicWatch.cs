using System.Globalization;
using System.Text;

namespace OpsisNewsCrawler.Core;

/// <summary>Local, user-defined topic and region rules. No remote classification or telemetry.</summary>
public sealed class TopicWatch
{
    public string Name { get; set; } = "";
    public List<string> Terms { get; set; } = new();
    public string Region { get; set; } = "";
    public bool Enabled { get; set; } = true;
}

public sealed record TopicMatch(TopicWatch Topic, NewsItem Item, int Score);

public static class TopicMatcher
{
    public static IReadOnlyList<TopicMatch> Find(IEnumerable<NewsItem> news, IEnumerable<TopicWatch> topics)
    {
        // Terms and regions are normalised once, not once per story (1000 stories x 50 topics x 20 terms); null terms from a hand-edited config are ignored.
        var catalog = topics.Where(t => t.Enabled && !string.IsNullOrWhiteSpace(t.Name))
            .Take(50)
            .Select(t => (Topic: t,
                Terms: (t.Terms ?? new()).Where(x => x is not null && x.Trim().Length >= 3).Take(20).Select(x => Breaking.Normalize(x.Trim()).ToLowerInvariant()).ToArray(),
                Region: Breaking.Normalize((t.Region ?? "").Trim()).ToLowerInvariant(),
                RegionUsable: (t.Region ?? "").Trim().Length > 1))
            .ToArray();
        var found = new List<TopicMatch>();
        foreach (var item in news.Where(n => n.Kind == NewsItemKind.News).Take(1000))
        {
            var content = Breaking.Normalize(item.Title + " " + (item.Summary ?? "")).ToLowerInvariant();
            foreach (var entry in catalog)
            {
                if (entry.Terms.Length == 0) continue;
                var hit = entry.Terms.Count(t => content.Contains(t, StringComparison.Ordinal));
                if (hit == 0) continue;
                // Region is only a textual relevance hint, not a verified geolocation.
                var regionMatch = entry.RegionUsable && content.Contains(entry.Region, StringComparison.Ordinal);
                found.Add(new TopicMatch(entry.Topic, item, Math.Min(100, hit * 20 + (regionMatch ? 15 : 0))));
            }
        }
        return found.OrderByDescending(x => x.Score).ThenByDescending(x => x.Item.PublishedAt).Take(300).ToArray();
    }
}
