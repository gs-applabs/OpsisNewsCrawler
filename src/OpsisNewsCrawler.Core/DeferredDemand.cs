namespace OpsisNewsCrawler.Core;

/// <summary>One delayed retry per live demand token. A canceled generation never
/// prevents a replacement from scheduling or clears the replacement's marker.</summary>
public sealed class DeferredDemand
{
    private readonly object gate = new();
    private object? pending;
    private CancellationToken pendingToken;

    public async Task RunAsync(CancellationToken token, TimeSpan delay, Action run)
    {
        object marker = new();
        lock (gate)
        {
            if (token.IsCancellationRequested || (pending is not null && !pendingToken.IsCancellationRequested)) return;
            pending = marker;
            pendingToken = token;
        }
        try
        {
            await Task.Delay(delay, token);
            if (!token.IsCancellationRequested) run();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        finally
        {
            lock (gate)
                if (ReferenceEquals(pending, marker)) { pending = null; pendingToken = default; }
        }
    }
}
