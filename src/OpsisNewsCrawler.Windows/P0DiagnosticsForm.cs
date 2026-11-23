using OpsisNewsCrawler.Core;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace OpsisNewsCrawler.Windows;

/// <summary>Visible-only diagnostics: source health, memory and cache writes; no extra network polling.</summary>
internal sealed class P0DiagnosticsForm : Form
{
    [DllImport("user32.dll")] private static extern int GetGuiResources(IntPtr process, int flags);
    private readonly TickerHost host;
    private TimeSpan previousCpu;
    private long previousSample;
    private readonly System.Windows.Forms.Timer refresh = new() { Interval = 4000 };
    private readonly Label summary = new() { Dock = DockStyle.Top, Height = 58, AutoEllipsis = true };
    private readonly ListView rows = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true };

    public P0DiagnosticsForm(TickerHost host)
    {
        this.host = host;
        Text = Loc.T("diag.title");
        Size = new Size(950, 540);
        MinimumSize = new Size(640, 340);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9.5f);
        rows.Columns.Add(Loc.T("diag.source"), 340);
        rows.Columns.Add(Loc.T("diag.lastSuccess"), 145);
        rows.Columns.Add(Loc.T("diag.failures"), 75);
        rows.Columns.Add(Loc.T("diag.nextAttempt"), 145);
        rows.Columns.Add(Loc.T("diag.error"), 240);
        Controls.Add(rows);
        Controls.Add(summary);
        Theme.Changed += OnThemeChanged;
        refresh.Tick += (_, _) => UpdateValues();
        Shown += (_, _) => { OnThemeChanged(); UpdateValues(); refresh.Start(); };
        VisibleChanged += (_, _) => { if (!Visible) refresh.Stop(); };
    }

    private void OnThemeChanged()
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(new Action(OnThemeChanged)); return; }
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        rows.BackColor = Theme.Surface;
        rows.ForeColor = Theme.Text;
        summary.BackColor = Theme.Background;
        summary.ForeColor = Theme.Text;
        rows.Invalidate();
    }

    private void UpdateValues()
    {
        if (IsDisposed) return;
        using var process = Process.GetCurrentProcess();
        var mem = process.PrivateMemorySize64 / (1024.0 * 1024);
        int gdi = -1;
        try { gdi = GetGuiResources(process.Handle, 0); } catch (DllNotFoundException) { }
        var disk = DataCache.WriteDiagnostics;
        var sample = Stopwatch.GetTimestamp();
        var cpu = process.TotalProcessorTime;
        var percent = previousSample <= 0 ? 0 : 100 * (cpu - previousCpu).TotalSeconds /
            (Stopwatch.GetElapsedTime(previousSample, sample).TotalSeconds * Math.Max(1, Environment.ProcessorCount));
        previousSample = sample;
        previousCpu = cpu;
        summary.Text = Loc.T("diag.metrics", percent, mem, gdi, disk.Writes, disk.Skips, host.RunningRefreshes);
        var health = host.FeedHealth;
        rows.BeginUpdate();
        try
        {
            rows.Items.Clear();
            foreach (var h in health)
            {
                string When(DateTimeOffset? v) => v?.ToLocalTime().ToString("dd/MM HH:mm") ?? "—";
                rows.Items.Add(new ListViewItem(new[] { h.Url, When(h.LastOk), h.ConsecutiveFailures.ToString(),
                    When(h.NextRetryAt), h.LastError ?? "" }));
            }
        }
        finally { rows.EndUpdate(); }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            refresh.Stop();
            refresh.Dispose();
            Theme.Changed -= OnThemeChanged;
        }
        base.Dispose(disposing);
    }
}
