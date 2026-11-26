using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

/// <summary>0.30.0 analytics: pure measures over existing data; missing data gives no number.</summary>
public sealed class Analytics030Tests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

    private static NewsItem Item(string title, string host, DateTimeOffset at, string? summary = null) =>
        new(title, $"https://{host}/{Math.Abs(title.GetHashCode())}", host, at, summary);

    private static DatedClose[] Closes(params decimal[] values) =>
        values.Select((v, i) => new DatedClose(new DateOnly(2026, 1, 1).AddDays(i), v)).ToArray();

    [Fact]
    public void SeriesSkipsBadRowsAndKeepsOnePerDay()
    {
        var s = MarketMetrics.Series(new[]
        {
            new Candle("2026-01-02", 1, 1, 1, 11, true), new Candle("2026-01-01", 1, 1, 1, 10, true), new Candle("bad", 1, 1, 1, 9, true),
            new Candle("2026-01-02", 1, 1, 1, 12, true), new Candle("2026-01-03", 1, 1, 1, 0, true)
        });
        Assert.Equal(new[] { 10m, 12m }, s.Select(x => x.Close));
    }

    [Fact]
    public void VolatilityNeedsEnoughSamplesAndIsZeroForAFlatSeries()
    {
        Assert.Null(MarketMetrics.AnnualisedVolatility(Closes(10, 11, 12)));
        var flat = Closes(Enumerable.Repeat(10m, 30).ToArray());
        Assert.Equal(0, MarketMetrics.AnnualisedVolatility(flat)!.AnnualisedPercent);
        var jumpy = Closes(Enumerable.Range(0, 30).Select(i => i % 2 == 0 ? 10m : 11m).ToArray());
        Assert.True(MarketMetrics.AnnualisedVolatility(jumpy)!.AnnualisedPercent > 50);
    }

    [Fact]
    public void DrawdownIsPeakToLaterTrough()
    {
        var d = MarketMetrics.MaxDrawdown(Closes(10, 20, 15, 25, 10, 30))!;
        Assert.Equal(60, d.Percent);                                  // 25 -> 10
        Assert.Equal(new DateOnly(2026, 1, 4), d.Peak);
        Assert.Equal(new DateOnly(2026, 1, 5), d.Trough);
        Assert.Equal(0, MarketMetrics.MaxDrawdown(Closes(1, 2, 3))!.Percent);
        Assert.Null(MarketMetrics.MaxDrawdown(Closes(5)));
    }

    [Fact]
    public void ComparisonUsesOnlyCommonDays()
    {
        var a = Closes(100, 110, 121);
        var b = new[] { new DatedClose(new DateOnly(2026, 1, 1), 200), new DatedClose(new DateOnly(2026, 1, 3), 210) };
        var c = MarketMetrics.Compare(a, b)!;
        Assert.Equal(21, c.SeriesPercent);
        Assert.Equal(5, c.BenchmarkPercent);
        Assert.Equal(2, c.CommonDays);
        Assert.Null(MarketMetrics.Compare(a, new[] { new DatedClose(new DateOnly(2030, 1, 1), 1) }));
    }

    [Fact]
    public void AllocationNeverMixesCurrenciesAndCountsUnvalued()
    {
        var r = MarketMetrics.Allocation(new (string, string, decimal?)[] { ("A", "EUR", 30), ("B", "eur", 70), ("C", "USD", 5), ("D", "EUR", null) });
        Assert.Equal(1, r.Unvalued);
        Assert.Equal(3, r.Valued);
        Assert.Equal(70, r.Rows.Single(x => x.Name == "B").PercentOfCurrency);
        Assert.Equal(100, r.Rows.Single(x => x.Name == "C").PercentOfCurrency);
    }

    [Fact]
    public void TimeWeightedReturnRemovesTheEffectOfMoneyMovedIn()
    {
        // +10 %, then a deposit of 100 and +10 % again: 21 % whatever the deposit
        var points = new[]
        {
            new ValuationPoint(new DateOnly(2026, 1, 1), 100),
            new ValuationPoint(new DateOnly(2026, 2, 1), 210, 100),
            new ValuationPoint(new DateOnly(2026, 3, 1), 231)
        };
        Assert.Equal(21, MarketMetrics.TimeWeightedReturnPercent(points));
        Assert.Null(MarketMetrics.TimeWeightedReturnPercent(points.Take(1).ToArray()));
        Assert.Null(MarketMetrics.TimeWeightedReturnPercent(new[] { points[1], points[0] }));                        // wrong order
        Assert.Null(MarketMetrics.TimeWeightedReturnPercent(new[] { new ValuationPoint(new DateOnly(2026, 1, 1), 0), points[2] }));
    }

    private static NewsItem[] Sample() => new[]
    {
        Item("European Commission proposes new budget for Angela Merkel Foundation", "a.example", Now.AddHours(-1)),
        Item("Angela Merkel Foundation welcomes European Commission plan", "b.example", Now.AddHours(-3)),
        Item("European Commission delays the vote", "c.example", Now.AddHours(-5)),
        Item("Earthquake in Japan, no damage", "c.example", Now.AddHours(-2)),
        Item("Japan markets rise", "d.example", Now.AddHours(-30))
    };

    [Fact]
    public void EntitiesAndRelationsKeepTheirEvidence()
    {
        var entities = NewsAnalytics.Entities(Sample());
        var commission = entities.Single(e => e.Name == "European Commission");
        Assert.Equal(3, commission.Reports);
        Assert.Equal(3, commission.Links.Count);
        var link = Assert.Single(NewsAnalytics.Relations(Sample()));
        Assert.Equal(2, link.Reports);
        Assert.Contains("European Commission", new[] { link.A, link.B });
    }

    [Fact]
    public void TrendCoveragePlacesAndNearMove()
    {
        var trend = NewsAnalytics.Trend(Sample(), "japan", Now, buckets: 4, hoursPerBucket: 12);
        Assert.Equal(4, trend.Count);
        Assert.Equal(1, trend[^1].Reports);
        Assert.Equal(2, trend.Sum(t => t.Reports));
        Assert.All(NewsAnalytics.Trend(Sample(), "  ", Now), t => Assert.Equal(0, t.Reports));
        Assert.NotEmpty(NewsAnalytics.Coverage(Sample()));
        var japan = NewsAnalytics.Places(Sample()).Single(p => p.CountryCode == "JP");
        Assert.Equal(2, japan.Reports);
        Assert.Single(NewsAnalytics.NearMove(Sample(), "Japan", Now, TimeSpan.FromHours(6)));
        Assert.Empty(NewsAnalytics.NearMove(Sample(), "x", Now));
    }

    [Fact]
    public void CalendarCategoriesFilterAndImport()
    {
        var json = "[{\"Id\":\"1\",\"Title\":\"ACME dividend\",\"Country\":\"GR\",\"StartsAt\":\"2026-12-01T09:00:00Z\",\"Source\":\"ACME\",\"Category\":\"dividend-ex\",\"Unit\":\"EUR\",\"Period\":\"2026\"}," +
                   "{\"Id\":\"2\",\"Title\":\"CPI\",\"Country\":\"GR\",\"StartsAt\":\"2026-12-02T09:00:00Z\",\"Source\":\"Stats\",\"Category\":\"macro\",\"Revised\":\"2.1\"}]";
        var events = EconomicCalendar.ImportJson(json);
        Assert.Equal("EUR", events[0].Unit);
        Assert.Equal("2.1", events[1].Revised);
        Assert.Single(EconomicCalendar.Select(events, Now, 30, 0, null, category: "macro"));
        Assert.Equal(2, EconomicCalendar.Select(events, Now, 30, 0, null).Count);
    }

    [Fact]
    public void ArchiveStoresSearchesRecordsChangesAndRespectsLimits()
    {
        var path = Path.Combine(Path.GetTempPath(), "opsis-arch-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var archive = new NewsArchive(path);
            var story = new NewsItem("Original headline about the port", "https://n.example/port", "N", Now.AddHours(-2), "first text");
            archive.Observe(new[] { story, new NewsItem("local", "http://192.168.1.1/x", "L", Now) }, Now);
            Assert.Equal(1, archive.Count);                                         // a local address is never kept
            Assert.True(File.Exists(path));
            archive.Observe(new[] { story with { Title = "Corrected headline about the port" } }, Now.AddMinutes(1));
            var change = Assert.Single(archive.Changes);
            Assert.Equal("Original headline about the port", change.OldTitle);
            Assert.Single(archive.Search(new NewsQuery("corrected port")));
            archive.Flush(Now.AddMinutes(2));
            Assert.Equal(1, new NewsArchive(path).Count);                           // survives a restart
            archive.Observe(Array.Empty<NewsItem>(), Now.AddDays(NewsArchive.RetentionDays + 1));
            Assert.Equal(0, archive.Count);                                          // retention
            archive.Clear();
            Assert.False(File.Exists(path));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}

public sealed class GeoHomeCountryTests
{
    [Theory]
    [InlineData("Wildfire in Greece forces evacuations")]
    [InlineData("Καιρός σε όλη την Ελλάδα")]
    public void GreeceIsRecognised(string title) =>
        Assert.Equal("GR", NewsGeo.Hint(new NewsItem(title, "https://a.example/1", "A", DateTimeOffset.UtcNow))?.CountryCode);
}

public sealed class DenialDifferenceTests
{
    [Fact]
    public void ADenialByOnlySomeOutletsIsShownAsADifference()
    {
        var now = DateTimeOffset.UtcNow;
        var a = new NewsItem("Ministry confirms the new tax plan", "https://a.example/1", "A", now);
        var b = new NewsItem("Ministry denies the new tax plan", "https://b.example/1", "B", now);
        var cluster = new StoryCluster(a.Url, a.Title, now, new[] { a, b }, 2);
        var diff = Assert.Single(StoryAnalysis.Differences(cluster));
        Assert.Equal("denial", diff.Kind);
        Assert.Single(diff.Sources);
        var both = new StoryCluster(b.Url, b.Title, now, new[] { b, b with { Url = "https://c.example/1", Source = "C" } }, 2);
        Assert.Empty(StoryAnalysis.Differences(both));
    }
}
