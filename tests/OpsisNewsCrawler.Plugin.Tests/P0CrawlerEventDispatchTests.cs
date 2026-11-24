using OpsisNewsCrawler.Plugin.Abstractions;
using OpsisNewsCrawler.Windows;
using Xunit;

namespace OpsisNewsCrawler.Plugin.Tests;

public sealed class P0CrawlerEventDispatchTests
{
    [Fact]
    public async Task DeliversEventsWithoutBlockingProducerAndStopsAfterDispose()
    {
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var dispatch = new CrawlerEventDispatch(item => completion.TrySetResult(item.Title));
        var message = new SuiteEvent(SuiteEventKind.News, "crawler", "k1", "First", DateTimeOffset.UtcNow);
        Assert.True(dispatch.TryPublish(message));
        Assert.Equal("First", await completion.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        dispatch.Dispose();
        Assert.False(dispatch.TryPublish(message));
    }

    [Fact]
    public void RejectsUnboundedQueueSizes()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CrawlerEventDispatch(_ => { }, 2048));
    }
}
