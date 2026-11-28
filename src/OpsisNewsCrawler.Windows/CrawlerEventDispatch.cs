using System.Threading.Channels;
using OpsisNewsCrawler.Plugin.Abstractions;

namespace OpsisNewsCrawler.Windows;

/// <summary>
/// One bounded single-reader event pump per active Crawler host. A slow or
/// malfunctioning SuiteEventHub subscriber cannot block ticker painting or
/// turn a flood of updates into unbounded background tasks and memory usage.
/// No disk writes. Stops on the plugin Stop and is recreated on Restart.
/// </summary>
internal sealed class CrawlerEventDispatch : IDisposable
{
    private readonly Channel<SuiteEvent> channel;
    private readonly CancellationTokenSource stop = new();
    private readonly Task pump;
    private int disposed;

    internal CrawlerEventDispatch(Action<SuiteEvent> publish, int capacity = 128)
    {
        ArgumentNullException.ThrowIfNull(publish);
        if (capacity is < 1 or > 1024) throw new ArgumentOutOfRangeException(nameof(capacity));
        channel = Channel.CreateBounded<SuiteEvent>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true, SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest,
            AllowSynchronousContinuations = false
        });
        pump = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in channel.Reader.ReadAllAsync(stop.Token))
                {
                    if (Volatile.Read(ref disposed) != 0) break;
                    try { publish(item); }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Crawler event observer error: " + ex);
                    }
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        });
    }

    internal bool TryPublish(SuiteEvent message) =>
        Volatile.Read(ref disposed) == 0 && channel.Writer.TryWrite(message);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        stop.Cancel();
        channel.Writer.TryComplete();
        _ = pump.ContinueWith(_ => stop.Dispose(), CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        // Do not await the subscriber on the host UI thread: it may be stuck.
        // The pump has a bounded queue and self-disposes its token on exit.
    }
}
