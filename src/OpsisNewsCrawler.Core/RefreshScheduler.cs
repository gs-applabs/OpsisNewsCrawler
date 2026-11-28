namespace OpsisNewsCrawler.Core;

/// <summary>
/// Single-flight lifecycle for periodic refreshes. A fast timer never starts a second copy of
/// an operation while its prior invocation is still running; unrelated operations may run in parallel.
/// Work uses the caller's cancellation token. Exceptions are observed, not left on fire-and-forget tasks.
/// </summary>
public sealed class RefreshScheduler : IDisposable
{
    private readonly object gate = new();
    private readonly HashSet<string> running = new(StringComparer.OrdinalIgnoreCase);
    private bool disposed;
    private readonly Dictionary<string, (Func<Task> Work, CancellationToken Token)> latest = new(StringComparer.OrdinalIgnoreCase);

    public event Action<string, Exception>? Failed;

    public int RunningCount { get { lock (gate) return running.Count; } }

    public bool TryRun(string key, Func<Task> work, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(work);
        lock (gate)
        {
            if (disposed || cancellationToken.IsCancellationRequested || !running.Add(key)) return false;
        }
        _ = ExecuteAsync(key, work, cancellationToken);
        return true;
    }

    /// <summary>For activation or changed demand only: retain one latest request while the old
    /// operation winds down. Ordinary timer ticks still use TryRun and never queue.</summary>
    public bool RunLatest(string key, Func<Task> work, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(work);
        lock (gate)
        {
            if (disposed || cancellationToken.IsCancellationRequested) return false;
            if (!running.Add(key))
            {
                latest[key] = (work, cancellationToken);
                return true;
            }
        }
        _ = ExecuteAsync(key, work, cancellationToken);
        return true;
    }

    private async Task ExecuteAsync(string key, Func<Task> work, CancellationToken ct)
    {
        while (true)
        {
            try
            {
                if (!ct.IsCancellationRequested) await work();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            catch (Exception ex)
            {
                try { Failed?.Invoke(key, ex); }
                catch (Exception handlerError) { System.Diagnostics.Debug.WriteLine(handlerError); }
                System.Diagnostics.Debug.WriteLine($"RefreshScheduler {key}: {ex}");
            }
            lock (gate)
            {
                if (!disposed && latest.Remove(key, out var next) && !next.Token.IsCancellationRequested)
                {
                    (work, ct) = next;
                    continue;
                }
                latest.Remove(key);
                running.Remove(key);
                return;
            }
        }
    }

    public void Dispose()
    {
        lock (gate) { disposed = true; latest.Clear(); }
    }
}
