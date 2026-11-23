using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public sealed class P0RefreshSchedulerTests
{
    [Fact]
    public async Task PreventsOverlappingOperationsButAllowsIndependentKeys()
    {
        using var scheduler = new RefreshScheduler();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.True(scheduler.TryRun("news", async () => { entered.SetResult(); await release.Task; }));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(scheduler.TryRun("news", () => Task.CompletedTask));
        Assert.True(scheduler.TryRun("quotes", () => Task.CompletedTask));
        release.SetResult();
        Assert.True(SpinWait.SpinUntil(() => scheduler.RunningCount == 0, 2000));
        Assert.True(scheduler.TryRun("news", () => Task.CompletedTask));
    }

    [Fact]
    public void CancellationAndDisposalDoNotScheduleMoreWork()
    {
        using var scheduler = new RefreshScheduler();
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        Assert.False(scheduler.TryRun("weather", () => Task.CompletedTask, cancel.Token));
        scheduler.Dispose();
        Assert.False(scheduler.TryRun("weather", () => Task.CompletedTask));
    }

    [Fact]
    public async Task ExceptionsAreObservedAndSchedulerCanRecover()
    {
        using var scheduler = new RefreshScheduler();
        var signaled = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        scheduler.Failed += (_, e) => signaled.TrySetResult(e);
        Assert.True(scheduler.TryRun("news", () => throw new InvalidOperationException("failure")));
        Assert.IsType<InvalidOperationException>(await signaled.Task.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal(0, scheduler.RunningCount);
        Assert.True(scheduler.TryRun("news", () => Task.CompletedTask));
    }
}
