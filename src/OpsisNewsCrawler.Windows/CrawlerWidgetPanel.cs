using OpsisNewsCrawler.Core;
using OpsisNewsCrawler.Plugin.Abstractions;
using System.Diagnostics;

namespace OpsisNewsCrawler.Windows;

/// <summary>
/// Opt-in desktop widget with the existing Opsis theme tokens. Data is drawn from the
/// Crawler's in-memory state; it has no hidden network polling, and a hidden widget stops its timer.
/// </summary>
public sealed class CrawlerWidgetPanel : UserControl
{
    private readonly TickerHost host;
    private readonly string kind;
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 30_000 };
    private readonly Label heading = new() { Dock = DockStyle.Top, Height = 36, AutoEllipsis = true };
    private readonly ListBox items = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, IntegralHeight = false };
    private readonly List<NewsItem> visible = new();
    private int refreshQueued;
    private readonly IDisposable eventSubscription;

    public CrawlerWidgetPanel(TickerHost host, string kind)
    {
        this.host = host ?? throw new ArgumentNullException(nameof(host));
        this.kind = kind;
        Dock = DockStyle.Fill;
        MinimumSize = new Size(210, 110);
        Font = new Font("Segoe UI", 9.5f);
        heading.Text = kind.ToUpperInvariant();
        heading.Padding = new Padding(12, 6, 0, 0);
        heading.AccessibleRole = AccessibleRole.StaticText;
        items.AccessibleRole = AccessibleRole.List;
        items.AccessibleName = "Opsis " + kind + " feed";
        items.SelectedIndexChanged += (_, _) =>
        {
            var selected = items.SelectedIndex;
            items.AccessibleDescription = selected >= 0 && selected < visible.Count
                ? $"{visible[selected].Title}. {visible[selected].Summary}" : heading.Text;
        };
        items.DoubleClick += (_, _) => OpenSelected();
        items.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { OpenSelected(); e.Handled = true; } };
        Controls.Add(items);
        Controls.Add(heading);
        Theme.Changed += ApplyTheme;
        Loc.Changed += RefreshSafe;
        timer.Tick += (_, _) => RefreshSafe();
        eventSubscription = SuiteEventHub.Subscribe(e =>
        {
            if (e.Kind.ToString().Equals(kind switch { "markets" => "Market", "history" => "Calendar", _ => kind }, StringComparison.OrdinalIgnoreCase))
                RequestRefresh();
        }, replay: false);
        ApplyTheme();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        RefreshSafe();
        if (Visible) timer.Start();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (IsDisposed) return;
        if (Visible && IsHandleCreated) { timer.Start(); RefreshSafe(); }
        else timer.Stop();
    }

    private void ApplyTheme()
    {
        if (IsDisposed) return;
        if (InvokeRequired) { if (IsHandleCreated) BeginInvoke(new Action(ApplyTheme)); return; }
        BackColor = Theme.Surface;
        ForeColor = Theme.Text;
        heading.ForeColor = Theme.Accent;
        heading.BackColor = Theme.Surface;
        items.BackColor = Theme.Surface;
        items.ForeColor = Theme.Text;
        Invalidate();
    }

    // SuiteEventHub observers run off the UI thread. One pending BeginInvoke at
    // most: a breaking-news burst must not enqueue thousands of UI messages.
    private void RequestRefresh()
    {
        if (IsDisposed || !IsHandleCreated || !Visible || Interlocked.Exchange(ref refreshQueued, 1) != 0)
            return;
        try
        {
            BeginInvoke(new Action(() =>
            {
                Interlocked.Exchange(ref refreshQueued, 0);
                if (!IsDisposed) RefreshSafe();
            }));
        }
        catch (InvalidOperationException) { Interlocked.Exchange(ref refreshQueued, 0); }
    }

    private void RefreshSafe()
    {
        if (IsDisposed || !Visible || !IsHandleCreated) return;
        if (InvokeRequired)
        {
            try { BeginInvoke(new Action(RefreshSafe)); }
            catch (InvalidOperationException) { }
            return;
        }
        var profile = WidgetStudio.Resolve(host.Config.WidgetProfiles, kind);
        visible.Clear();
        visible.AddRange(host.WidgetSnapshot(kind).Take(profile.MaxItems));
        items.BeginUpdate();
        try
        {
            items.Items.Clear();
            foreach (var line in visible)
                items.Items.Add(WidgetStudio.FormatLine(line.Title, line.Source, line.PublishedAt, profile));
            items.ItemHeight = profile.Layout == "context" ? 26 : 23;
            heading.Text = WidgetStudio.Title(kind, Loc.IsGreek);
            items.AccessibleDescription = visible.Count == 0
                ? (Loc.IsGreek ? "Δεν υπάρχουν διαθέσιμα δεδομένα" : "No data available")
                : string.Join(". ", visible.Take(3).Select(x => x.Title));
        }
        finally { items.EndUpdate(); }
    }

    private void OpenSelected()
    {
        var index = items.SelectedIndex;
        if (index < 0 || index >= visible.Count) return;
        var url = visible[index].Url;
        if (!NetGuard.IsPublicHttpUrl(url)) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timer.Stop(); timer.Dispose();
            eventSubscription.Dispose();
            Theme.Changed -= ApplyTheme;
            Loc.Changed -= RefreshSafe;
        }
        base.Dispose(disposing);
    }
}
