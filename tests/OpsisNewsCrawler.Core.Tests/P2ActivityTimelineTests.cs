using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public sealed class P2ActivityTimelineTests
{
    [Fact]
    public void RepeatedUnchangedEventsAreNotRewritten()
    {
        var timeline = new ActivityTimeline();
        var item = new ActivityItem("crawler\u001fBreaking\u001fone","Breaking","crawler",
            "Breaking headline", DateTimeOffset.UtcNow,"https://example.org","summary");
        Assert.True(timeline.Add(item));
        Assert.False(timeline.Add(item with { At = item.At.AddSeconds(30) }));
        Assert.Equal(1,timeline.Count);
        Assert.True(timeline.Add(item with { Title = "Updated headline" }));
        Assert.Single(timeline.Snapshot());
    }

    [Fact]
    public void JournalIsBoundedAndCategoryFilterIsChronological()
    {
        var timeline = new ActivityTimeline();
        var at = new DateTimeOffset(2026,11,23,12,0,0,TimeSpan.Zero);
        for(var i=0;i<620;i++)
            timeline.Add(new ActivityItem("key-"+i,i%2==0?"News":"Calendar",
                "test","Event "+i,at.AddMinutes(i),null,null));
        Assert.Equal(ActivityTimeline.MaxEntries,timeline.Count);
        var news = timeline.Snapshot("News",7,at.AddDays(1));
        Assert.All(news,x=>Assert.Equal("News",x.Kind));
        Assert.True(news.Zip(news.Skip(1),(a,b)=>a.At>=b.At).All(x=>x));
    }

    [Fact]
    public void LoadedEntriesStayUnique()
    {
        var timeline = new ActivityTimeline();
        var item = new ActivityItem("key","Market","test","Price",DateTimeOffset.UtcNow,null,null);
        timeline.Load(new[]{item,item});
        Assert.Equal(1,timeline.Count);
    }
}
