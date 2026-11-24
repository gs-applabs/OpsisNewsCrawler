using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;
public sealed class P2FeaturesTests
{
    private static readonly DateTimeOffset Day = new(2026,11,23,12,0,0,TimeSpan.Zero);
    [Fact] public void TopicWatchFindsKeywordsWithLocalRegionBoost()
    {
        var item = new NewsItem("Earthquake in Athens", "https://example.org/a","Source",Day);
        var topic = new TopicWatch { Name="Seismic",Terms=new(){"earthquake"},Region="Athens" };
        Assert.Equal(35,Assert.Single(TopicMatcher.Find(new[]{item},new[]{topic})).Score);
    }
    [Fact] public void StoryExplorerClustersSimilarTitlesButNotDistantEvents()
    {
        var items = new[] {
            new NewsItem("Markets in Europe rebound after Thursday session","https://example.org/a","A",Day),
            new NewsItem("Markets in Europe rebound after Thursday session","https://example.net/b","B",Day.AddMinutes(-5)),
            new NewsItem("Markets in Europe rebound after Thursday session","https://example.com/c","C",Day.AddDays(-3))
        };
        var g=StoryExplorer.Cluster(items);
        Assert.Equal(2,g.Count); Assert.Equal(2,g[0].Reports.Count);
        Assert.Equal(2,g[0].PublisherGroups);
    }
    [Fact] public void DigestEncodesHtmlAndNeverIncludesJavascriptUrls()
    {
        var news = new NewsItem("<script>alert(1)</script>","javascript:alert(2)","A",Day,"<img src=x>");
        var html=NewsDigest.Html(new[]{news},new DateOnly(2026,11,23),"Daily");
        Assert.DoesNotContain("<script>",html);
        Assert.DoesNotContain("javascript:alert",html);
        Assert.Contains("&lt;img",html);
    }
    [Fact] public void TimelinePreservesAncientYearWithoutChangingTimestamp()
    {
        var e = new TimelineEvent("490","Battle","Calendar",Day,-490,"chronikologio",null);
        var line=Assert.Single(SuiteTimeline.Build(Array.Empty<NewsItem>(),new[]{e}));
        Assert.Equal(-490,line.HistoricalYear);
        Assert.Equal(Day,line.At);
    }
    [Fact] public void LedgerComputesFeesAndRealizedWeightedCostAndSplit()
    {
        var entries=new[] {
            new PortfolioTransaction("ABC",Day,PortfolioAction.Buy,10,100,5),
            new PortfolioTransaction("ABC",Day.AddHours(1),PortfolioAction.Sell,4,120,2),
            new PortfolioTransaction("ABC",Day.AddHours(2),PortfolioAction.Split,2,0),
            new PortfolioTransaction("ABC",Day.AddHours(3),PortfolioAction.Dividend,1,20,1)
        };
        var p=PortfolioLedger.Calculate(entries);
        Assert.Equal(12,p.Units);
        Assert.Equal(603,p.CostBasis);
        Assert.Equal(76,p.RealizedPnl);
        Assert.Equal(19,p.Dividends);
        Assert.Equal(8,p.Fees);
    }
    [Fact] public void OversellingThrows() =>
        Assert.Throws<InvalidOperationException>(()=>PortfolioLedger.Calculate(new[]{
            new PortfolioTransaction("X",Day,PortfolioAction.Sell,1,1)}));
}
