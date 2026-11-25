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

    private async Task ExecuteAsync(string key, Func<Task> work, CancellationToken ct)
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
        finally { lock (gate) running.Remove(key); }
    }

    public void Dispose()
    {
        lock (gate) disposed = true;
    }
}
