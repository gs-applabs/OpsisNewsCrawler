using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public sealed class RefreshSchedulerActivationTests
{
    [Fact]
    public async Task RapidActivationChangesKeepOneLatestRequestAndNeverOverlap()
    {
        using var scheduler = new RefreshScheduler();
        using var original = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0, concurrent = 0, peak = 0;
        Assert.True(scheduler.TryRun("news", async () =>
        {
            peak = Math.Max(peak, Interlocked.Increment(ref concurrent));
            entered.SetResult();
            await release.Task;
            Interlocked.Decrement(ref concurrent);
        }, original.Token));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        original.Cancel();
        for (int i = 0; i < 1000; i++)
            Assert.True(scheduler.RunLatest("news", () =>
            {
                Interlocked.Increment(ref calls);
                peak = Math.Max(peak, Interlocked.Increment(ref concurrent));
                Interlocked.Decrement(ref concurrent);
                completed.TrySetResult();
                return Task.CompletedTask;
            }));
        Assert.False(scheduler.TryRun("news", () => throw new Exception("timer must not queue")));
        Assert.Equal(0, calls);
        release.SetResult();
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, calls);
        Assert.Equal(1, peak);
    }
}
