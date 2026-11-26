using System.Globalization;
using System.Text;

namespace OpsisNewsCrawler.Core;

/// <summary>Local, user-defined topic and region rules. No remote classification or telemetry.</summary>
public sealed class TopicWatch
{
    /// <summary>Stable identity (N01): survives renames and reordering, so filters and exports can refer to a topic. Older settings get one on load.</summary>
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<string> Terms { get; set; } = new();
    public string Region { get; set; } = "";
    public bool Enabled { get; set; } = true;

    /// <summary>Gives every topic a unique short id; keeps the ones that are already valid and unique.</summary>
    public static void EnsureStableIds(IList<TopicWatch> topics)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var t in topics)
        {
            var id = (t.Id ?? "").Trim();
            if (id.Length is < 4 or > 32 || !id.All(c => char.IsAsciiLetterOrDigit(c) || c == '-') || !seen.Add(id))
            {
                do id = Guid.NewGuid().ToString("N")[..12]; while (!seen.Add(id));
            }
            t.Id = id;
        }
    }
}

public sealed record TopicMatch(TopicWatch Topic, NewsItem Item, int Score);

/// <summary>Optional narrowing of the matches (N01). Every field that is null/empty is ignored.</summary>
public sealed record TopicFilter(IReadOnlySet<string>? TopicIds = null, string? Region = null, int MinScore = 0, string? Source = null, DateTimeOffset? Since = null);

public static class TopicMatcher
{
    public static IReadOnlyList<TopicMatch> Find(IEnumerable<NewsItem> news, IEnumerable<TopicWatch> topics, TopicFilter? filter = null)
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
        if (filter is not null) found = found.Where(m => Passes(m, filter)).ToList();
        return found.OrderByDescending(x => x.Score).ThenByDescending(x => x.Item.PublishedAt).Take(300).ToArray();
    }

    private static bool Passes(TopicMatch m, TopicFilter f) =>
        (f.TopicIds is not { Count: > 0 } || (!string.IsNullOrEmpty(m.Topic.Id) && f.TopicIds.Contains(m.Topic.Id)))
        && m.Score >= f.MinScore
        && (f.Since is null || m.Item.PublishedAt >= f.Since)
        && (string.IsNullOrWhiteSpace(f.Source) || string.Equals(m.Item.Source, f.Source, StringComparison.OrdinalIgnoreCase))
        && (string.IsNullOrWhiteSpace(f.Region) || string.Equals((m.Topic.Region ?? "").Trim(), f.Region.Trim(), StringComparison.OrdinalIgnoreCase));
}
