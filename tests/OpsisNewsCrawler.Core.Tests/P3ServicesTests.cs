using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;
public sealed class P3ServicesTests
{
    private static readonly DateTimeOffset Now=new(2026,11,23,12,0,0,TimeSpan.Zero);
    [Fact]public void EconomicCalendarOnlyIncludesFutureAndEscapesIcs()
    {
        var records=new[]{
            new EconomicEvent("1","GDP; preliminary","GR",Now.AddDays(1),"Office"),
            new EconomicEvent("2","Past","CY",Now.AddDays(-1),"Office")
        };
        var e=Assert.Single(EconomicCalendar.Upcoming(records,Now));
        var ics=EconomicCalendar.ExportIcs(new[]{e});
        Assert.Contains("BEGIN:VEVENT",ics);Assert.Contains("GDP\\; preliminary",ics);
        Assert.DoesNotContain("SUMMARY:Past",ics);
    }
    [Fact]public void EvidenceAssistantDoesNotMakeTruthClaims()
    {
        var record=new NewsItem("Example","https://example.org/story","Source",Now);
        var review=FactCheckSupport.Review(new StoryCluster("a","Example",Now,new[]{record},1));
        Assert.Single(review.Links);Assert.Contains("not independent verification",review.Caution);
    }
    [Fact]public void FinancePolicyKeeps15MinuteMinimumAndHonorsBlocks()
    {
        Assert.False(FinanceSourcePolicy.MayPoll(true,false,Now,Now.AddMinutes(-12)));
        Assert.True(FinanceSourcePolicy.MayPoll(true,false,Now,Now.AddMinutes(-15)));
        Assert.False(FinanceSourcePolicy.MayPoll(true,true,Now,null));
    }
    [Fact]public void MediaAssetsNeverTreatLocalUrlsAsSafe()
    {
        Assert.False(MediaSuiteBridge.IsSafeRemoteAsset(new MediaSuiteAsset("https://127.0.0.1/internal","x",null,null)));
        Assert.True(MediaSuiteBridge.IsSafeRemoteAsset(new MediaSuiteAsset("https://example.org/img.png","x","Publisher","image/png")));
    }
}
