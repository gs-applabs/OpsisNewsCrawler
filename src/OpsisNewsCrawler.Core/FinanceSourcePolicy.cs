namespace OpsisNewsCrawler.Core;

/// <summary>One responsible schedule for optional market data adapters (including Yahoo/yfinance feeds).</summary>
public static class FinanceSourcePolicy
{
    public static readonly TimeSpan MinimumQuoteInterval=TimeSpan.FromMinutes(15);

    public static bool MayPoll(bool optedIn, bool providerBlocked, DateTimeOffset now,
        DateTimeOffset? lastRequested)
    {
        if(!optedIn||providerBlocked)return false;
        return lastRequested is null || now-lastRequested.Value>=MinimumQuoteInterval;
    }

    public static string Describe(string provider, bool delayed, DateTimeOffset? quotedAt)
        => $"{provider} · {(delayed ? "delayed/reference" : "reported live")} · "+
           (quotedAt?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'")??"unknown timestamp");
}
