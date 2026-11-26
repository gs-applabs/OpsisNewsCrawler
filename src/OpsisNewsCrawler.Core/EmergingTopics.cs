namespace OpsisNewsCrawler.Core;

/// <summary>A word that suddenly appears in many headlines of several publishers. A measure of coverage, never of truth or importance.</summary>
public sealed record EmergingTopic(string Term, int RecentReports, int RecentPublishers, double BaselinePerWindow, double Ratio);

/// <summary>
/// N02: compares the last window (default 6 hours) with the 24 hours before it, from the headlines the program already holds (no request, no store).
/// A term counts only when at least <see cref="MinPublishers"/> different publishers used it in the recent window and its rate is clearly above the baseline.
/// </summary>
public static class EmergingTopics
{
    public const int MinPublishers = 3;
    private const double MinRatio = 3.0;
    private const int MaxResults = 10;

    public static IReadOnlyList<EmergingTopic> Find(IEnumerable<NewsItem> news, DateTimeOffset now, TimeSpan? window = null)
    {
        ArgumentNullException.ThrowIfNull(news);
        var recentSpan = TimeSpan.FromHours(Math.Clamp((window ?? TimeSpan.FromHours(6)).TotalHours, 1, 24));
        var baselineSpan = TimeSpan.FromHours(24);
        var recentFrom = now - recentSpan;
        var baselineFrom = recentFrom - baselineSpan;

        var items = news.Where(n => n.Kind == NewsItemKind.News && n.PublishedAt <= now.AddMinutes(5) && n.PublishedAt >= baselineFrom && !string.IsNullOrWhiteSpace(n.Title))
            .Take(3000).ToList();
        if (items.Count == 0) return Array.Empty<EmergingTopic>();

        var recent = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);   // term -> publishers
        var recentCount = new Dictionary<string, int>(StringComparer.Ordinal);
        var baseline = new Dictionary<string, int>(StringComparer.Ordinal);
        var recentTotal = 0;
        foreach (var item in items)
        {
            var isRecent = item.PublishedAt >= recentFrom;
            if (isRecent) recentTotal++;
            var publisher = PublisherIdentity.Canonical(BreakingEngine.Publisher(item));
            foreach (var word in HeadlineDedup.Words(item.Title))
            {
                if (word.Length < 4 || word.All(char.IsDigit)) continue;
                if (isRecent)
                {
                    if (!recent.TryGetValue(word, out var set)) recent[word] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (publisher.Length > 0) set.Add(publisher);
                    recentCount[word] = recentCount.GetValueOrDefault(word) + 1;
                }
                else baseline[word] = baseline.GetValueOrDefault(word) + 1;
            }
        }
        if (recentTotal < MinPublishers) return Array.Empty<EmergingTopic>();

        var scale = recentSpan / baselineSpan;   // baseline counts are per 24 h; scale them to one recent window
        var result = new List<EmergingTopic>();
        foreach (var (term, publishers) in recent)
        {
            if (publishers.Count < MinPublishers) continue;
            var count = recentCount[term];
            if (count > recentTotal * 0.5) continue;   // a word in half of all headlines is a feed habit, not a topic
            var expected = baseline.GetValueOrDefault(term) * scale;
            var ratio = count / Math.Max(expected, 1.0);
            if (ratio >= MinRatio) result.Add(new EmergingTopic(term, count, publishers.Count, Math.Round(expected, 2), Math.Round(ratio, 1)));
        }
        return result.OrderByDescending(r => r.RecentPublishers).ThenByDescending(r => r.Ratio).ThenBy(r => r.Term, StringComparer.Ordinal).Take(MaxResults).ToArray();
    }
}
