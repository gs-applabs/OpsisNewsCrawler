using System.Text.Json;
using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

/// <summary>0.29.0 news intelligence (N01-N10): extensions of the existing topic matcher and story explorer, pure and offline.</summary>
public sealed class NewsIntelligenceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

    private static NewsItem Item(string title, string host, DateTimeOffset at, string? summary = null, string? source = null) =>
        new(title, $"https://{host}/a/{Math.Abs(title.GetHashCode())}", source ?? host, at, summary);

    private static StoryCluster Cluster(params NewsItem[] reports) =>
        new(reports[0].Url, reports[0].Title, reports.Max(r => r.PublishedAt), reports, reports.Select(r => r.Source).Distinct().Count());

    // ---- N01 ----
    [Fact]
    public void OldSettingsGetUniqueStableTopicIdsAndKeepThem()
    {
        var json = "{\"TopicWatches\":[{\"Name\":\"A\",\"Terms\":[\"athens\"]},{\"Name\":\"B\",\"Terms\":[\"paris\"],\"Id\":\"dup-id-1\"},{\"Name\":\"C\",\"Terms\":[\"rome\"],\"Id\":\"dup-id-1\"}]}";
        var path = Path.Combine(Path.GetTempPath(), "opsis-cfg-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(path, json);
            var cfg = AppConfig.Load(path);
            var ids = cfg.TopicWatches.Select(t => t.Id).ToList();
            Assert.Equal(3, ids.Distinct().Count());
            Assert.All(ids, id => Assert.InRange(id.Length, 4, 32));
            Assert.Equal("dup-id-1", cfg.TopicWatches[1].Id);   // a valid unique id is kept
            Assert.True(cfg.TrySave(path));
            Assert.Equal(ids, AppConfig.Load(path).TopicWatches.Select(t => t.Id));   // stable across save and load
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void TopicFilterNarrowsBySourceScoreRegionAndId()
    {
        var a = new TopicWatch { Id = "topic-aaa", Name = "Quakes", Terms = new() { "earthquake" }, Region = "Athens" };
        var b = new TopicWatch { Id = "topic-bbb", Name = "Markets", Terms = new() { "stocks" } };
        var news = new[]
        {
            Item("Earthquake near Athens felt in the city", "one.example", Now.AddHours(-1), source: "One"),
            Item("Stocks rally after the session", "two.example", Now.AddHours(-2), source: "Two"),
            Item("Earthquake drill in Crete", "two.example", Now.AddDays(-3), source: "Two")
        };
        Assert.Equal(3, TopicMatcher.Find(news, new[] { a, b }).Count);
        Assert.Equal(2, TopicMatcher.Find(news, new[] { a, b }, new TopicFilter(TopicIds: new HashSet<string> { "topic-aaa" })).Count);
        Assert.Single(TopicMatcher.Find(news, new[] { a, b }, new TopicFilter(Source: "Two", Since: Now.AddDays(-1))));
        Assert.Single(TopicMatcher.Find(news, new[] { a, b }, new TopicFilter(Region: "athens", MinScore: 35)));
    }

    // ---- N02 ----
    [Fact]
    public void EmergingTopicNeedsSeveralPublishersAndAClearRise()
    {
        var items = new List<NewsItem>();
        for (var i = 0; i < 4; i++) items.Add(Item($"Wildfire spreads near the village number {i}", $"pub{i}.example", Now.AddHours(-1).AddMinutes(-i)));
        // a habitual word: in the baseline every day and now
        for (var i = 0; i < 30; i++) items.Add(Item($"Weather report for the region today {i}", $"pub{i % 5}.example", Now.AddHours(-10).AddMinutes(-i * 20)));
        for (var i = 0; i < 4; i++) items.Add(Item($"Weather report for the region now {i}", $"pub{i}.example", Now.AddMinutes(-30 - i)));
        var topics = EmergingTopics.Find(items, Now);
        Assert.Contains(topics, t => t.Term == "wildfire" && t.RecentPublishers >= 3);
        Assert.DoesNotContain(topics, t => t.Term == "weather");
        Assert.Empty(EmergingTopics.Find(items.Take(2), Now));   // too little to say anything
    }

    // ---- N03 ----
    [Fact]
    public void TimelineIsInPublicationOrderAndFoldsTheSameLink()
    {
        var first = Item("Bridge collapse reported in the north", "a.example", Now.AddHours(-3));
        var later = Item("Bridge collapse: rescue teams arrive", "b.example", Now.AddHours(-1));
        var undated = new NewsItem("Bridge collapse analysis", "https://c.example/x", "C", DateTimeOffset.MinValue);
        var timeline = StoryAnalysis.Timeline(Cluster(later, first, first, undated));
        Assert.Equal(new[] { "a.example", "b.example" }, timeline.Select(t => t.Source));
    }

    // ---- N04 ----
    [Fact]
    public void DifferingNumbersAreShownNotResolved()
    {
        var a = Item("Flood leaves 5 dead in the valley", "a.example", Now);
        var b = Item("Flood: 7 dead in the valley, officials say", "b.example", Now);
        var diff = Assert.Single(StoryAnalysis.Differences(Cluster(a, b)));
        Assert.Equal("number", diff.Kind);
        Assert.Equal(new[] { "5", "7" }, diff.Values);
        Assert.Equal(2, diff.Sources.Count);
        var same = Item("Flood leaves 5 dead in the valley region", "c.example", Now);
        Assert.Empty(StoryAnalysis.Differences(Cluster(a, same)));
        Assert.Empty(StoryAnalysis.Differences(Cluster(a)));   // one publisher: nothing to compare
    }

    // ---- N05 ----
    [Fact]
    public void AgencyAttributionAndIdenticalTextAreCommonOrigin()
    {
        var a = Item("Minister resigns after talks (Reuters)", "a.example", Now);
        var b = Item("Minister resigns after talks - Reuters", "b.example", Now);
        var origin = Assert.Single(StoryAnalysis.CommonOrigins(Cluster(a, b)));
        Assert.Equal("Reuters", origin.Agency);
        Assert.Equal(2, origin.Reports.Count);

        var text = "The government confirmed on Thursday that the new tax package will be voted next week in parliament";
        var c = Item("Tax package to be voted", "c.example", Now, text);
        var d = Item("Tax package vote next week", "d.example", Now, text);
        var same = Assert.Single(StoryAnalysis.CommonOrigins(Cluster(c, d)));
        Assert.Null(same.Agency);
        Assert.Empty(StoryAnalysis.CommonOrigins(Cluster(a)));
    }

    // ---- N08 ----
    [Fact]
    public void GeoHintNeedsOneUnambiguousCountry()
    {
        var france = NewsGeo.Hint(Item("President of France visits the flood area", "a.example", Now));
        Assert.NotNull(france);
        Assert.Equal("FR", france!.CountryCode);
        Assert.Null(NewsGeo.Hint(Item("Talks between France and Germany continue", "a.example", Now)));   // two countries: unknown
        Assert.Null(NewsGeo.Hint(Item("Local council approves the new budget", "a.example", Now)));       // none: unknown
        Assert.Null(NewsGeo.Hint(Item("Georgia primary results", "a.example", Now)));                      // ambiguous name: unknown
    }

    // ---- N10 ----
    [Fact]
    public void SearchMatchesAllTermsIgnoringAccentsAndRanksTitleHitsFirst()
    {
        var news = new[]
        {
            Item("Σεισμός στην Κρήτη: τι λένε οι ειδικοί", "a.example", Now.AddHours(-5), source: "A"),
            Item("Ειδήσεις της ημέρας", "b.example", Now.AddHours(-1), "Μικρός σεισμός στην Κρήτη χθες", source: "B"),
            Item("Σεισμός στην Ιαπωνία", "c.example", Now, source: "C")
        };
        var result = NewsSearch.Find(news, new NewsQuery("σεισμος κρητη"));
        Assert.Equal(new[] { "A", "B" }, result.Select(r => r.Source));        // title hit first, then the summary hit
        Assert.Single(NewsSearch.Find(news, new NewsQuery("σεισμος", Source: "C")));
        Assert.Single(NewsSearch.Find(news, new NewsQuery("σεισμος", From: Now.AddMinutes(-30))));
        Assert.Equal(3, NewsSearch.Find(news, new NewsQuery()).Count);
        Assert.True(NewsSearch.Find(Enumerable.Range(0, 1000).Select(i => Item("Earthquake " + i, "x.example", Now)), new NewsQuery("earthquake")).Count <= NewsSearch.MaxResults);
    }
}
