namespace OpsisNewsCrawler.Core;

/// <summary>Bounded, deterministic exponential backoff for unreliable RSS sources.</summary>
public static class FeedRetryPolicy
{
    public static TimeSpan Delay(int consecutiveFailures, TimeSpan? serverRetryAfter = null)
    {
        var seconds = Math.Min(30 * Math.Pow(2, Math.Clamp(consecutiveFailures - 1, 0, 6)), 1800);
        if (serverRetryAfter is { } requested && requested > TimeSpan.Zero)
            seconds = Math.Max(seconds, Math.Min(requested.TotalSeconds, 1800));
        return TimeSpan.FromSeconds(seconds);
    }

    /// <summary>
    /// N34: extra waiting time after a SUCCESSFUL download of a source that has published nothing for a long time: 15 minutes when its newest story is 7+ days old,
    /// 1 hour at 30+ days; none for an active source or an unknown/future date. Never longer, so a revived source is noticed within the hour.
    /// </summary>
    public static TimeSpan? QuietDelay(DateTimeOffset? newestStory, DateTimeOffset now)
    {
        if (newestStory is not { } newest || newest > now) return null;
        var age = now - newest;
        if (age >= TimeSpan.FromDays(30)) return TimeSpan.FromHours(1);
        if (age >= TimeSpan.FromDays(7)) return TimeSpan.FromMinutes(15);
        return null;
    }
}
