using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using OpsisNewsCrawler.Core;

namespace OpsisNewsCrawler.Windows;

/// <summary>Explicit CI-only probe. No timer, data collection or disk output in normal use.</summary>
public sealed class ResourceProbe : IDisposable
{
    [DllImport("user32.dll")] private static extern int GetGuiResources(IntPtr process, int flags);
    private readonly TickerHost host;
    private readonly string path;
    private readonly int duration;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly Process process = Process.GetCurrentProcess();
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 5000 };
    private readonly List<string> rows = new()
    {
        "elapsed_seconds,cpu_percent_all_cores,private_bytes,working_set_bytes,managed_bytes,gdi_handles,user_handles,handles,threads,refresh_jobs,frames,rss_bytes,cache_writes,cache_skips,file_writes,file_payload_bytes,active_services,refresh_timers_active,updates_active,market_initialized,bar_visible"
    };
    private TimeSpan previousCpu;
    private double previousSeconds;
    private bool finished;

    public ResourceProbe(TickerHost host, string path, int duration)
    {
        this.host = host;
        this.path = path;
        this.duration = Math.Clamp(duration, 30, 14400);
        previousCpu = process.TotalProcessorTime;
        timer.Tick += (_, _) => Sample();
        timer.Start();
    }

    private void Sample()
    {
        process.Refresh();
        var elapsed = clock.Elapsed.TotalSeconds;
        var cpu = process.TotalProcessorTime;
        var percent = 100 * (cpu - previousCpu).TotalSeconds / Math.Max(.001, elapsed - previousSeconds) / Math.Max(1, Environment.ProcessorCount);
        previousCpu = cpu;
        previousSeconds = elapsed;
        var disk = DataCache.WriteDiagnostics;
        var files = SafeFile.Diagnostics;
        rows.Add(FormattableString.Invariant($"{elapsed:F2},{percent:F3},{process.PrivateMemorySize64},{process.WorkingSet64},{GC.GetTotalMemory(false)},{GetGuiResources(process.Handle, 0)},{GetGuiResources(process.Handle, 1)},{process.HandleCount},{process.Threads.Count},{host.RunningRefreshes},{host.Window.RenderedFrames},{host.NetworkReceivedBytes},{disk.Writes},{disk.Skips},{files.Writes},{files.Bytes},{host.ActiveRefreshServices.Count()},{(host.HasActiveRefreshTimers ? 1 : 0)},{(host.AutomaticUpdatesActive ? 1 : 0)},{(host.MarketStoreInitialized ? 1 : 0)},{(host.Window.Visible ? 1 : 0)}"));
        if (elapsed < duration) return;
        timer.Stop();
        finished = true;
        Application.ExitThread();
    }

    public void Dispose()
    {
        timer.Stop();
        timer.Dispose();
        process.Dispose();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllLines(path, rows);
        if (!finished) Log.Write("Resource probe stopped before requested duration");
    }
}
