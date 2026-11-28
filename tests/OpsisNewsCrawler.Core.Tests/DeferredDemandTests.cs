using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public sealed class DeferredDemandTests
{
    [Fact]
    public async Task DuplicateRetryIsIgnoredAndCanceledDemandNeverRuns()
    {
        var retry = new DeferredDemand();
        using var old = new CancellationTokenSource();
        var calls = 0;
        var first = retry.RunAsync(old.Token, TimeSpan.FromMinutes(1), () => Interlocked.Increment(ref calls));
        await retry.RunAsync(old.Token, TimeSpan.Zero, () => Interlocked.Increment(ref calls));
        Assert.Equal(0, calls);
        old.Cancel();
        using var replacement = new CancellationTokenSource();
        await retry.RunAsync(replacement.Token, TimeSpan.Zero, () => Interlocked.Increment(ref calls));
        await first;
        Assert.Equal(1, calls);
        await retry.RunAsync(replacement.Token, TimeSpan.Zero, () => Interlocked.Increment(ref calls));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task AlreadyCanceledDemandDoesNotSchedule()
    {
        var retry = new DeferredDemand();
        using var token = new CancellationTokenSource();
        token.Cancel();
        await retry.RunAsync(token.Token, TimeSpan.FromMinutes(1), () => throw new InvalidOperationException());
    }
}
