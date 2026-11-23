using OpsisNewsCrawler.Core;
using SuiteEventHub = OpsisNewsCrawler.Core.SuiteEventHub;
using SuiteEvent = OpsisNewsCrawler.Core.SuiteEvent;
using SuiteEventKind = OpsisNewsCrawler.Core.SuiteEventKind;
using OpsisNewsCrawler.Plugin.Abstractions;
using Xunit;

namespace OpsisNewsCrawler.Plugin.Tests;

[Collection("plugin-feeds")]
public sealed class P1EventHubTests
{
    [Fact]
    public void EventsAreDeduplicatedAndObserversCanUnsubscribe()
    {
        var origin = "test-" + Guid.NewGuid().ToString("N");
        var title = "Headline " + origin;
        var one = new SuiteEvent(SuiteEventKind.News, origin, "key-1", title, DateTimeOffset.UtcNow);
        int seen = 0;
        using (SuiteEventHub.Subscribe(e => { if (e.Origin == origin) seen++; }, replay: false))
        {
            SuiteEventHub.Publish(one);
            SuiteEventHub.Publish(one with { At = one.At.AddSeconds(5) });
            Assert.Equal(1, seen);
            SuiteEventHub.Publish(one with { Title = title + " updated" });
            Assert.Equal(2, seen);
        }
        SuiteEventHub.Publish(one with { Title = title + " after dispose" });
        Assert.Equal(2, seen);
    }

    [Fact]
    public void SubscriberFaultNeverPreventsOtherSubscribers()
    {
        var origin = "fault-" + Guid.NewGuid().ToString("N");
        var count = 0;
        using var fail = SuiteEventHub.Subscribe(_ => throw new InvalidOperationException(), replay: false);
        using var pass = SuiteEventHub.Subscribe(e => { if (e.Origin == origin) count++; }, replay: false);
        SuiteEventHub.Publish(new SuiteEvent(SuiteEventKind.Calendar, origin, "c", "History", DateTimeOffset.UtcNow, HistoricalYear: -490));
        Assert.Equal(1, count);
    }

    [Fact]
    public void LegacyContractIdentityIsUntouched()
    {
        Assert.Equal(new Version(0,8,0,0), typeof(PluginFeeds).Assembly.GetName().Version);
        Assert.NotNull(typeof(IOpsisNewsCrawlerPlugin).GetMethod("GetTrayMenuItems"));
    }
}
