namespace OpsisNewsCrawler.Core;

/// <summary>What a source is doing, for the health display (N33): a temporary failure is not the same as a source that has been silent or dead for a long time.</summary>
public enum FeedActivity { Unknown, Working, TemporaryFailure, Silent, Dormant }

/// <summary>
/// Classifies a source from the data the RSS engine already keeps (no new requests, no new files).
/// It only describes; nothing is switched off automatically - the user decides.
/// </summary>
public static class FeedActivityPolicy
{
    /// <summary>Works, but its newest story is older than this: the source is probably abandoned.</summary>
    public static readonly TimeSpan SilentAfter = TimeSpan.FromDays(30);
    /// <summary>Has not answered successfully for this long while failing: not a passing outage any more.</summary>
    public static readonly TimeSpan DormantAfter = TimeSpan.FromDays(14);

    public static FeedActivity Classify(FeedHealth health, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(health);
        if (health.LastOk is null && health.LastTry is null) return FeedActivity.Unknown;
        if (health.ConsecutiveFailures > 0)
        {
            // The state lives in memory (nothing new is written to disk), so this sees the current run: a source that
            // has failed for two weeks or more since its last success is dormant; anything shorter is a passing failure.
            var reference = health.LastOk ?? health.LastTry!.Value;
            return now - reference >= DormantAfter ? FeedActivity.Dormant : FeedActivity.TemporaryFailure;
        }
        if (health.NewestItemAt is { } newest && newest <= now && now - newest >= SilentAfter) return FeedActivity.Silent;
        return health.LastOk is null ? FeedActivity.Unknown : FeedActivity.Working;
    }
}
