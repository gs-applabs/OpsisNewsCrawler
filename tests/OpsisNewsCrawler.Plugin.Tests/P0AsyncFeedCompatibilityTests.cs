using OpsisNewsCrawler.Plugin.Abstractions;
using Xunit;

namespace OpsisNewsCrawler.Plugin.Tests;

[Collection("plugin-feeds")]
public sealed class P0AsyncFeedCompatibilityTests
{
    private static PluginRichFeedItem Item(int number) => new("id" + number, "Event " + number);

    [Fact]
    public async Task SnapshotUpdatesInBackgroundButHostGetterIsAlwaysSynchronous()
    {
        using var publisher = new AsyncRichFeedSnapshot("async-test-1", "Χρονικό", "Timeline", 5);
        Assert.Empty(publisher.GetItems());
        Assert.True(await publisher.RefreshAsync(_ => Task.FromResult<IReadOnlyList<PluginRichFeedItem>>(
            Enumerable.Range(0, 100).Select(Item).ToList())));
        try
        {
            var feed = PluginFeeds.FindRich("async-test-1");
            Assert.NotNull(feed);
            Assert.Equal(5, feed!.GetItems().Count);
            Assert.Equal("Event 0", feed.GetItems()[0].Text);
            Assert.Equal(5, publisher.GetItems().Count);
        }
        finally { PluginFeeds.Unregister("async-test-1"); }
    }

    [Fact]
    public async Task ConcurrentRefreshDoesNotOverwriteLastGoodData()
    {
        using var publisher = new AsyncRichFeedSnapshot("async-test-2", "Ειδήσεις", "News");
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = publisher.RefreshAsync(async _ =>
        {
            entered.TrySetResult();
            await gate.Task;
            return new[] { Item(1) };
        });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(await publisher.RefreshAsync(_ =>
            Task.FromResult<IReadOnlyList<PluginRichFeedItem>>(new[] { Item(99) })));
        gate.SetResult();
        try
        {
            Assert.True(await first);
            Assert.Equal("Event 1", Assert.Single(publisher.GetItems()).Text);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                publisher.RefreshAsync(_ => throw new InvalidOperationException("network")));
            Assert.Equal("Event 1", Assert.Single(publisher.GetItems()).Text);
        }
        finally { PluginFeeds.Unregister("async-test-2"); }
    }

    [Fact]
    public async Task LegacyFeedSignaturesAndAssemblyIdentityRemainUnchanged()
    {
        Assert.Equal(new Version(0, 8, 0, 0), typeof(PluginFeeds).Assembly.GetName().Version);
        Assert.NotNull(typeof(PluginFeed).GetConstructor(new[]
        {
            typeof(string), typeof(string), typeof(string), typeof(Func<IReadOnlyList<PluginFeedItem>>)
        }));
        Assert.NotNull(typeof(PluginRichFeed).GetConstructor(new[]
        {
            typeof(string), typeof(string), typeof(string), typeof(Func<IReadOnlyList<PluginRichFeedItem>>)
        }));
        using var publisher = new AsyncRichFeedSnapshot("compat-test", "EL", "EN");
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            publisher.RefreshAsync(_ => Task.FromResult<IReadOnlyList<PluginRichFeedItem>>(new[] { Item(0) }), cancel.Token));
        Assert.Empty(publisher.GetItems());
    }
}
