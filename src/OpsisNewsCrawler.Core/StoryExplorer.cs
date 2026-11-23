namespace OpsisNewsCrawler.Core;

/// <summary>A cluster of headlines, not a claim that the facts are true or independently verified.</summary>
public sealed record StoryCluster(string Key, string Headline, DateTimeOffset LastUpdate,
    IReadOnlyList<NewsItem> Reports, int PublisherGroups);

public static class StoryExplorer
{
    public static IReadOnlyList<StoryCluster> Cluster(IEnumerable<NewsItem> news,
        TimeSpan? maxSeparation = null)
    {
        var window = maxSeparation ?? TimeSpan.FromHours(12);
        var groups = new List<List<NewsItem>>();
        foreach (var item in news.Where(n => n.Kind == NewsItemKind.News && !string.IsNullOrWhiteSpace(n.Title))
                     .OrderByDescending(n => n.PublishedAt).Take(600))
        {
            var words = HeadlineDedup.Words(item.Title);
            var group = groups.FirstOrDefault(g =>
                Math.Abs((g[0].PublishedAt - item.PublishedAt).TotalHours) <= window.TotalHours
                && HeadlineDedup.AreSameStory(HeadlineDedup.Words(g[0].Title), words));
            if (group is null) groups.Add(new List<NewsItem> { item });
            else if (!group.Any(n => string.Equals(n.Url, item.Url, StringComparison.OrdinalIgnoreCase)))
                group.Add(item);
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
