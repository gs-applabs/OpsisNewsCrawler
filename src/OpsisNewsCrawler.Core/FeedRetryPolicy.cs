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
}
