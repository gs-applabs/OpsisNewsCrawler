using OpsisNewsCrawler.Core;

namespace OpsisNewsCrawler.Windows;

/// <summary>One OS wait registration, no polling timer or visible HWND required.
/// The standalone process creates its PID-scoped event; setup verifies its image path.</summary>
public sealed class InstallerShutdownListener : IDisposable
{
    private readonly EventWaitHandle signal;
    private readonly RegisteredWaitHandle registration;
    private int disposed;
    public static string EventName(int pid) => @"Local\OpsisNewsCrawler.Shutdown." + pid;

    internal InstallerShutdownListener(string name, SynchronizationContext ui, Action close)
    {
        signal = new EventWaitHandle(false, EventResetMode.AutoReset, name, out var created);
        if (!created) { signal.Dispose(); throw new IOException("Shutdown event already exists."); }
        try
        {
            registration = ThreadPool.RegisterWaitForSingleObject(signal, (_, timedOut) =>
            {
                if (timedOut || Volatile.Read(ref disposed) != 0) return;
                ui.Post(_ => { if (Volatile.Read(ref disposed) == 0) close(); }, null);
            }, null, Timeout.Infinite, executeOnlyOnce: true);
        }
        catch { signal.Dispose(); throw; }
    }

    public static InstallerShutdownListener? TryCreate(SynchronizationContext ui, Action close)
    {
        try { return new InstallerShutdownListener(EventName(Environment.ProcessId), ui, close); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or WaitHandleCannotBeOpenedException)
        {
            Log.Write("Installer shutdown event unavailable: " + ex.GetType().Name);
            return null;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        registration.Unregister(null);
        signal.Dispose();
    }
}
