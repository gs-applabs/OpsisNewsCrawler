using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

/// <summary>N33: a passing failure is not the same as a source that is silent or dead for weeks.</summary>
public sealed class FeedActivityPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NeverTriedIsUnknownAndHealthyRecentFeedWorks()
    {
        Assert.Equal(FeedActivity.Unknown, FeedActivityPolicy.Classify(new FeedHealth("u", null, null, 0, null), Now));
        Assert.Equal(FeedActivity.Working, FeedActivityPolicy.Classify(
            new FeedHealth("u", Now.AddMinutes(-5), Now.AddMinutes(-5), 0, null, null, Now.AddHours(-3)), Now));
    }

    [Fact]
    public void WorkingButSilentFor30DaysIsSilent_FutureDatesAreIgnored()
    {
        Assert.Equal(FeedActivity.Silent, FeedActivityPolicy.Classify(
            new FeedHealth("u", Now, Now, 0, null, null, Now.AddDays(-31)), Now));
        Assert.Equal(FeedActivity.Working, FeedActivityPolicy.Classify(
            new FeedHealth("u", Now, Now, 0, null, null, Now.AddDays(-29)), Now));
        Assert.Equal(FeedActivity.Working, FeedActivityPolicy.Classify(
            new FeedHealth("u", Now, Now, 0, null, null, Now.AddDays(400)), Now));   // a wrong date in the future proves nothing
    }

    [Fact]
    public void FailureIsTemporaryUntilTwoWeeksWithoutSuccess()
    {
        Assert.Equal(FeedActivity.TemporaryFailure, FeedActivityPolicy.Classify(
            new FeedHealth("u", Now.AddDays(-2), Now, 3, "HTTP 503"), Now));
        Assert.Equal(FeedActivity.Dormant, FeedActivityPolicy.Classify(
            new FeedHealth("u", Now.AddDays(-15), Now, 9, "HTTP 404"), Now));
        Assert.Equal(FeedActivity.TemporaryFailure, FeedActivityPolicy.Classify(
            new FeedHealth("u", null, Now, 1, "timeout"), Now));   // never worked in this run, tried just now
    }
}
