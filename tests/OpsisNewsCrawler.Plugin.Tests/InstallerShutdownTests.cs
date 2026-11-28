using OpsisNewsCrawler.Windows;
using Xunit;

namespace OpsisNewsCrawler.Plugin.Tests;
public sealed class InstallerShutdownTests
{
    [Fact]
    public void ShutdownSignalWorksWithoutTickerHandleAndDispatchesOnlyOnce()
    {
        var name = @"Local\OpsisNewsCrawler.TestShutdown." + Guid.NewGuid().ToString("N");
        using var queue = new System.Collections.Concurrent.BlockingCollection<Action>();
        var context = new TestContext(queue);
        var calls = 0;
        using var listener = new InstallerShutdownListener(name, context, () => calls++);
        using var sender = EventWaitHandle.OpenExisting(name);
        sender.Set();
        Assert.True(queue.TryTake(out var action, 5000));
        Assert.Equal(0, calls);
        action!();
        Assert.Equal(1, calls);
        sender.Set();
        Assert.False(queue.TryTake(out _, 250));
    }
    private sealed class TestContext(System.Collections.Concurrent.BlockingCollection<Action> queue) : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) => queue.Add(() => d(state));
    }
}
