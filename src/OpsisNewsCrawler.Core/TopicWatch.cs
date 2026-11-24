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
        var catalog = topics.Where(t => t.Enabled && !string.IsNullOrWhiteSpace(t.Name))
            .Take(50).ToArray();
        var found = new List<TopicMatch>();
        foreach (var item in news.Where(n => n.Kind == NewsItemKind.News).Take(1000))
        {
            var content = Breaking.Normalize(item.Title + " " + (item.Summary ?? "")).ToLowerInvariant();
            foreach (var topic in catalog)
            {
                var terms = (topic.Terms ?? new()).Where(t => t.Trim().Length >= 3).Take(20).ToArray();
                if (terms.Length == 0) continue;
                var hit = terms.Count(t => content.Contains(Breaking.Normalize(t.Trim()).ToLowerInvariant(), StringComparison.Ordinal));
                if (hit == 0) continue;
                // Region is only a textual relevance hint, not a verified geolocation.
                var region = (topic.Region ?? "").Trim();
                var regionMatch = region.Length > 1 && content.Contains(Breaking.Normalize(region).ToLowerInvariant(), StringComparison.Ordinal);
                found.Add(new TopicMatch(topic, item, Math.Min(100, hit * 20 + (regionMatch ? 15 : 0))));
            }
        }
        return found.OrderByDescending(x => x.Score).ThenByDescending(x => x.Item.PublishedAt).Take(300).ToArray();
    }
}
