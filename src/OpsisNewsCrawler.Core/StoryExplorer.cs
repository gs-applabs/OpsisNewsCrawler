namespace OpsisNewsCrawler.Core;

/// <summary>A cluster of headlines, not a claim that the facts are true or independently verified.</summary>
public sealed record StoryCluster(string Key, string Headline, DateTimeOffset LastUpdate,
    IReadOnlyList<NewsItem> Reports, int PublisherGroups);

public static class StoryExplorer
{
    public static IReadOnlyList<StoryCluster> Cluster(IEnumerable<NewsItem> news,
        TimeSpan? maxSeparation = null)
    {
        var window = TimeSpan.FromHours(Math.Clamp((maxSeparation ?? TimeSpan.FromHours(12)).TotalHours, 1, 72));
        var groups = new List<List<NewsItem>>();
        var anchors = new List<HashSet<string>>();
        foreach (var item in news.Where(n => n.Kind == NewsItemKind.News && !string.IsNullOrWhiteSpace(n.Title))
                     .OrderByDescending(n => n.PublishedAt).Take(600))
        {
            var words = HeadlineDedup.Words(item.Title);
            var index = -1;
            for (var i = 0; i < groups.Count; i++)
            {
                if (groups[i][0].PublishedAt - item.PublishedAt > window) continue;
                if (HeadlineDedup.AreSameStory(anchors[i], words)) { index = i; break; }
            }
            if (index < 0)
            {
                groups.Add(new List<NewsItem> { item });
                anchors.Add(words);
            }
            else if (!groups[index].Any(n => !string.IsNullOrWhiteSpace(item.Url)
                ? string.Equals(n.Url, item.Url, StringComparison.OrdinalIgnoreCase)
                : string.Equals(n.Source, item.Source, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(n.Title, item.Title, StringComparison.OrdinalIgnoreCase)))
                groups[index].Add(item);
        }
        return groups.Select(g =>
        {
            var first = g[0];
            var publishers = g.Select(BreakingEngine.Publisher).Select(PublisherIdentity.Canonical)
                .Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            return new StoryCluster(first.Url, first.Title, g.Max(n => n.PublishedAt),
                g.OrderByDescending(n => n.PublishedAt).ToArray(), publishers);
        }).OrderByDescending(g => g.LastUpdate).ToArray();
    }
}
