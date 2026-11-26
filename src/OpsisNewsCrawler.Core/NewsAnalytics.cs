using System.Text.RegularExpressions;

namespace OpsisNewsCrawler.Core;

/// <summary>A name seen in headlines, with the reports that support it. It is a text pattern, not a verified identity (N09).</summary>
public sealed record NewsEntity(string Name, int Reports, int Publishers, IReadOnlyList<string> Links);

/// <summary>Two names that appear in the same headlines (N13); the links are the evidence.</summary>
public sealed record EntityLink(string A, string B, int Reports, IReadOnlyList<string> Links);

public sealed record TrendBucket(DateTimeOffset Start, int Reports, int Publishers);

public sealed record CoverageRow(string Headline, int Reports, int Publishers, DateTimeOffset Latest);

/// <summary>Publications per country and continent named in the text (N11, N12). Counts of publications, not of events.</summary>
public sealed record PlaceCount(string CountryCode, string CountryName, string ContinentKey, int Reports);

/// <summary>
/// Offline analytics over the news the program already holds (N09, N11-N15, N19). Everything is a count of publications with its sources;
/// none of it says that something is true, important or frequent in reality.
/// </summary>
public static class NewsAnalytics
{
    private const int MaxNews = 2000, MaxEntities = 30, MaxLinks = 20;

    // two or more capitalised words in a row: names of people, organisations, places and events as headlines write them
    private static readonly Regex Name = new(@"\p{Lu}[\p{L}\p{M}'’-]+(?:\s+(?:of|de|von|του|της)?\s*\p{Lu}[\p{L}\p{M}'’-]+){1,3}",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    private static readonly HashSet<string> Starters = new(StringComparer.OrdinalIgnoreCase)
    { "The", "A", "An", "In", "On", "At", "New", "How", "Why", "What", "Breaking", "Live", "Ο", "Η", "Το", "Στο", "Στη", "Στην", "Τα", "Οι", "Νέα", "Νέο", "Πώς", "Γιατί" };

    private static IEnumerable<string> Names(string title)
    {
        MatchCollection matches;
        try { matches = Name.Matches(title); } catch (RegexMatchTimeoutException) { yield break; }
        foreach (Match m in matches)
        {
            var words = m.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
            while (words.Count > 0 && Starters.Contains(words[0])) words.RemoveAt(0);   // "The European Commission" -> "European Commission"
            if (words.Count >= 2) yield return string.Join(' ', words);
        }
    }

    private static List<NewsItem> News(IEnumerable<NewsItem> news) =>
        news.Where(n => n.Kind == NewsItemKind.News && !string.IsNullOrWhiteSpace(n.Title)).Take(MaxNews).ToList();

    private static int Publishers(IEnumerable<NewsItem> items) =>
        items.Select(i => PublisherIdentity.Canonical(BreakingEngine.Publisher(i))).Where(p => p.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Count();

    private static IReadOnlyList<string> Links(IEnumerable<NewsItem> items) =>
        items.Select(i => i.Url).Where(u => NetGuard.IsPublicHttpUrl(u)).Distinct(StringComparer.OrdinalIgnoreCase).Take(10).ToArray();

    private static Dictionary<string, List<NewsItem>> EntityMap(List<NewsItem> items)
    {
        var map = new Dictionary<string, List<NewsItem>>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
            foreach (var name in Names(item.Title).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!map.TryGetValue(name, out var list)) map[name] = list = new List<NewsItem>();
                list.Add(item);
            }
        return map;
    }

    /// <summary>Names that at least two reports used, most reported first.</summary>
    public static IReadOnlyList<NewsEntity> Entities(IEnumerable<NewsItem> news) =>
        EntityMap(News(news)).Where(p => p.Value.Count >= 2)
            .Select(p => new NewsEntity(p.Key, p.Value.Count, Publishers(p.Value), Links(p.Value)))
            .OrderByDescending(e => e.Publishers).ThenByDescending(e => e.Reports).ThenBy(e => e.Name, StringComparer.Ordinal).Take(MaxEntities).ToArray();

    /// <summary>The strongest co-occurrences between the entities above (never more than twenty).</summary>
    public static IReadOnlyList<EntityLink> Relations(IEnumerable<NewsItem> news)
    {
        var map = EntityMap(News(news));
        var top = map.Where(p => p.Value.Count >= 2).OrderByDescending(p => p.Value.Count).Take(MaxEntities).ToList();
        var links = new List<EntityLink>();
        for (var i = 0; i < top.Count; i++)
            for (var j = i + 1; j < top.Count; j++)
            {
                var shared = top[i].Value.Intersect(top[j].Value).ToList();
                if (shared.Count >= 2) links.Add(new EntityLink(top[i].Key, top[j].Key, shared.Count, Links(shared)));
            }
        return links.OrderByDescending(l => l.Reports).ThenBy(l => l.A, StringComparer.Ordinal).Take(MaxLinks).ToArray();
    }

    /// <summary>How many reports mentioned a term per time bucket, newest last (N14, N15). The same text matching as the search.</summary>
    public static IReadOnlyList<TrendBucket> Trend(IEnumerable<NewsItem> news, string term, DateTimeOffset now, int buckets = 12, int hoursPerBucket = 2)
    {
        buckets = Math.Clamp(buckets, 1, 48);
        hoursPerBucket = Math.Clamp(hoursPerBucket, 1, 24);
        var matching = NewsSearch.Find(news, new NewsQuery(term));
        if (string.IsNullOrWhiteSpace(term)) matching = Array.Empty<NewsItem>();
        var result = new List<TrendBucket>();
        for (var i = buckets - 1; i >= 0; i--)
        {
            var end = now.AddHours(-i * hoursPerBucket);
            var start = end.AddHours(-hoursPerBucket);
            var inside = matching.Where(n => n.PublishedAt > start && n.PublishedAt <= end).ToList();
            result.Add(new TrendBucket(start, inside.Count, Publishers(inside)));
        }
        return result;
    }

    /// <summary>The stories with the widest coverage (N19): unique reports and publisher groups, not a truth score.</summary>
    public static IReadOnlyList<CoverageRow> Coverage(IEnumerable<NewsItem> news, int top = 20) =>
        StoryExplorer.Cluster(news).OrderByDescending(c => c.PublisherGroups).ThenByDescending(c => c.Reports.Count).ThenByDescending(c => c.LastUpdate)
            .Take(Math.Clamp(top, 1, 100)).Select(c => new CoverageRow(c.Headline, c.Reports.Count, c.PublisherGroups, c.LastUpdate)).ToArray();

    public static IReadOnlyList<PlaceCount> Places(IEnumerable<NewsItem> news) =>
        News(news).Select(NewsGeo.Hint).Where(h => h is not null).GroupBy(h => h!.CountryCode)
            .Select(g => new PlaceCount(g.Key, g.First()!.CountryName, g.First()!.ContinentKey, g.Count()))
            .OrderByDescending(p => p.Reports).ThenBy(p => p.CountryName, StringComparer.Ordinal).ToArray();

    /// <summary>N30: headlines that name an instrument within a few hours of a price move. A coincidence in time, never a cause.</summary>
    public static IReadOnlyList<NewsItem> NearMove(IEnumerable<NewsItem> news, string instrumentName, DateTimeOffset moveTime, TimeSpan? window = null)
    {
        if (string.IsNullOrWhiteSpace(instrumentName) || instrumentName.Trim().Length < 3) return Array.Empty<NewsItem>();
        var span = window ?? TimeSpan.FromHours(24);
        return NewsSearch.Find(news, new NewsQuery(instrumentName, From: moveTime - span, To: moveTime + span)).Take(20).ToArray();
    }
}
