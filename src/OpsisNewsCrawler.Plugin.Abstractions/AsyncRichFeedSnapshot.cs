namespace OpsisNewsCrawler.Plugin.Abstractions;

/// <summary>
/// Opt-in asynchronous data producer that exposes a synchronous immutable memory snapshot to
/// the **existing** PluginRichFeed/GetItems registry. No network I/O is ever done on the host UI thread.
/// Consumers built against older host contract DLLs should keep using PluginRichFeed alone until
/// the host and plugins deploy the updated 0.8.0.0 binary together.
/// </summary>
public sealed class AsyncRichFeedSnapshot : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly CancellationTokenSource shutdown = new();
    private IReadOnlyList<PluginRichFeedItem> current = Array.Empty<PluginRichFeedItem>();
    private int disposed;
    private readonly PluginRichFeed feed;

    public AsyncRichFeedSnapshot(string key, string titleEl, string titleEn, int maxItems = 500)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Key = key;
        MaxItems = Math.Clamp(maxItems, 1, 500);
        feed = new PluginRichFeed(key, titleEl, titleEn, GetItems);
    }

    public string Key { get; }
    public int MaxItems { get; }
    public PluginRichFeed Feed => feed;
    public IReadOnlyList<PluginRichFeedItem> GetItems() => Volatile.Read(ref current);

    /// <summary>
    /// Explicitly refreshes on the caller's schedule; concurrent refreshes coalesce.
    /// On success re-registers a rich feed so the host invalidates its one-minute cache.
    /// Failed/cancelled refreshes preserve the last known-good snapshot.
    /// </summary>
    public async Task<bool> RefreshAsync(Func<CancellationToken, Task<IReadOnlyList<PluginRichFeedItem>>> load,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(load);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, shutdown.Token);
        if (!await gate.WaitAsync(0, linked.Token).ConfigureAwait(false)) return false;
        try
        {
            var loaded = await load(linked.Token).ConfigureAwait(false);
            linked.Token.ThrowIfCancellationRequested();
            var snapshot = Array.AsReadOnly((loaded ?? Array.Empty<PluginRichFeedItem>())
                .Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Text)).Take(MaxItems).ToArray());
            if (Volatile.Read(ref disposed) != 0) return false;
            Volatile.Write(ref current, snapshot);
            PluginFeeds.RegisterRich(feed); // event dispatch and provider ordering are already handled by the legacy host
            return true;
        }
        finally { gate.Release(); }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        shutdown.Cancel();
        // gate/shutdown are deliberately not disposed while an in-flight loader can still observe
        // cancellation or release the semaphore. The plugin's owner should await its refresh tasks.
    }
}
