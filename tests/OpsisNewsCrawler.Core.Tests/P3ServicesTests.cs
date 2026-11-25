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
    [Fact]public void IcsLinesAreFoldedAt75OctetsWithoutSplittingCharacters()
    {
        var e=new EconomicEvent("9",new string('Ω',120),"GR",Now.AddDays(1),"Office");
        var ics=EconomicCalendar.ExportIcs(new[]{e});
        var lines=ics.Split("\r\n",StringSplitOptions.RemoveEmptyEntries);
        Assert.All(lines,l=>Assert.True(System.Text.Encoding.UTF8.GetByteCount(l)<=75,l));
        var summary=string.Concat(lines.SkipWhile(l=>!l.StartsWith("SUMMARY:")).TakeWhile((l,i)=>i==0||l.StartsWith(' ')).Select((l,i)=>i==0?l:l[1..]));
        Assert.Equal("SUMMARY:"+new string('Ω',120),summary);
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

    [Fact] public void CalendarImportRejectsNullAndMalformedRecordsWithoutCrashing()
    {
        const string json = """
          [
            null,
            {"Id":null,"Title":"No id","Country":"GR","Source":"Office","StartsAt":"2026-11-24T12:00:00+00:00"},
            {"Id":"2","Title":null,"Country":"GR","Source":"Office","StartsAt":"2026-11-24T12:00:00+00:00"},
            {"Id":"3","Title":" ","Country":"GR","Source":"Office","StartsAt":"2026-11-24T12:00:00+00:00"},
            {"Id":"ok","Title":"GDP","Country":"GR","Source":"Office","StartsAt":"2026-11-24T12:00:00+00:00"}
          ]
          """;
        var imported = EconomicCalendar.ImportJson(json);
        Assert.Equal("ok", Assert.Single(imported).Id);
        Assert.Contains("SUMMARY:GDP", EconomicCalendar.ExportIcs(imported));
    }

    [Fact] public void CalendarIcsSkipsInvalidRecords()
    {
        var events = new[] {
            new EconomicEvent("good", "GDP", "GR", Now, "Source"),
            new EconomicEvent("", "Should not export", "GR", Now, "Source")
        };
        var ics = EconomicCalendar.ExportIcs(events);
        Assert.Contains("SUMMARY:GDP", ics);
        Assert.DoesNotContain("Should not export", ics);
    }

    [Fact] public void EvidenceReviewExcludesLocalUrls()
    {
        var records = new[] {
            new NewsItem("Private", "http://127.0.0.1/admin", "Local", Now),
            new NewsItem("External", "https://example.org/story", "Public", Now)
        };
        var cluster = new StoryCluster("id", "Example", Now, records, 2);
        var review = FactCheckSupport.Review(cluster);
        Assert.Equal("External", Assert.Single(review.Reports).Title);
        Assert.Equal("https://example.org/story", Assert.Single(review.Links));
    }
}
