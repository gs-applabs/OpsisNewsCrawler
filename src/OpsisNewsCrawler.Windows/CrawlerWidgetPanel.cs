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
    private readonly FlowLayoutPanel gallery = new()
    {
        Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown,
        WrapContents = false, Padding = new Padding(6), Visible = false,
        AccessibleName = "Opsis Newsroom Gallery"
    };
    private readonly Label breakingBanner = new()
    {
        Dock = DockStyle.Top, Height = 30, Visible = false,
        TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(12, 0, 4, 0),
        AccessibleRole = AccessibleRole.StaticText
    };
    private string gallerySignature = "";
    private Font? scaledFont;
    private int appliedFontScale = -1;
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
                ? SafeAccessibilityText($"{visible[selected].Title}. {visible[selected].Summary}") : heading.Text;
        };
        items.DoubleClick += (_, _) => OpenSelected();
        items.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { OpenSelected(); e.Handled = true; } };
        Controls.Add(items);
        Controls.Add(gallery);
        Controls.Add(breakingBanner);
        Controls.Add(heading);
        gallery.Resize += (_, _) => ResizeGalleryCards();
        gallery.DpiChangedAfterParent += (_, _) => ResizeGalleryCards();
        Theme.Changed += ApplyTheme;
        Theme.HostAccentChanged += ApplyTheme;
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
        var accent = WidgetAccent();
        heading.ForeColor = GalleryColorContrast.ContrastRatio(accent, Theme.Surface) >= 4.5
            ? accent : Theme.Text;
        heading.BackColor = Theme.Surface;
        items.BackColor = Theme.Surface;
        items.ForeColor = Theme.Text;
        gallery.BackColor = Theme.Background;
        breakingBanner.BackColor = accent;
        breakingBanner.ForeColor = GalleryColorContrast.Foreground(accent);
        // Theme changes do not rebuild the data or create redundant network traffic.
        foreach (var card in gallery.Controls.OfType<Button>())
            StyleGalleryCard(card);
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
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            Interlocked.Exchange(ref refreshQueued, 0);
        }
    }

    private void RefreshSafe()
    {
        if (IsDisposed || !Visible || !IsHandleCreated) return;
        if (InvokeRequired)
        {
            try { BeginInvoke(new Action(RefreshSafe)); }
            catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException) { }
            return;
        }
        var profile = WidgetStudio.Resolve(host.Config.WidgetProfiles, kind);
        ApplyFontScale(profile.FontScalePercent);
        var galleryMode = profile.Layout == "gallery" && WidgetStudio.CanUseGallery(kind);
        items.Visible = !galleryMode;
        gallery.Visible = galleryMode;
        visible.Clear();
        visible.AddRange(host.WidgetSnapshot(kind).Take(profile.MaxItems));
        items.BeginUpdate();
        try
        {
            items.Items.Clear();
            foreach (var line in visible)
                items.Items.Add(WidgetStudio.FormatLine(line.Title, line.Source, line.PublishedAt,
                    profile, allowTime: kind != "history"));
            items.ItemHeight = profile.Layout == "context" ? 26 : 23;
            heading.Text = WidgetStudio.Title(kind, Loc.IsGreek);
            breakingBanner.Visible = kind == "breaking" && visible.Count > 0;
            if (breakingBanner.Visible)
                breakingBanner.Text = Loc.IsGreek ? $"  ΕΚΤΑΚΤΗ ΕΠΙΚΑΙΡΟΤΗΤΑ  ·  {visible.Count}" : $"  BREAKING NEWS  ·  {visible.Count}";
            if (galleryMode) RefreshGallery(profile);
            else if (gallery.Controls.Count != 0)
            {
                ClearGallery();
                gallerySignature = "";
            }
            items.AccessibleDescription = visible.Count == 0
                ? (Loc.IsGreek ? "Δεν υπάρχουν διαθέσιμα δεδομένα" : "No data available")
                : string.Join(". ", visible.Take(3).Select(x => x.Title));
        }
        finally { items.EndUpdate(); }
    }

    private void ApplyFontScale(int percent)
    {
        var safePercent = Math.Clamp(percent, 80, 160);
        if (appliedFontScale == safePercent) return;
        var size = Math.Clamp(9.5f * safePercent / 100f, 8f, 16f);
        var next = new Font("Segoe UI", size, FontStyle.Regular, GraphicsUnit.Point);
        var prior = scaledFont;
        scaledFont = next;
        appliedFontScale = safePercent;
        // One font object owned by this panel; existing cards inherit
        // gallery.Font, avoiding a new GDI font per item or timer tick.
        gallery.Font = next;
        items.Font = next;
        heading.Font = next;
        prior?.Dispose();
        ResizeGalleryCards();
    }

    private void RefreshGallery(WidgetStudioProfile profile)
    {
        // Fixed-size, in-memory signatures avoid rebuilding 30 WinForms controls
        // on each timer tick and reduce GDI pressure in an always-running host.
        var cards = visible.Select(x => WidgetStudio.GalleryCardText(
            x.Title, x.Summary, x.Source, x.PublishedAt, profile,
            Loc.IsGreek, x.Breaking || kind == "breaking", kind == "history",
            relatedReports: x.StoryReports?.Count ?? 0)).ToArray();
        var signature = string.Join("\u001e", cards) + "\u001f" +
            string.Join("\u001e", visible.Select(x => x.Url ?? ""));
        if (gallerySignature == signature && gallery.Controls.Count == cards.Length) return;
        gallerySignature = signature;
        gallery.SuspendLayout();
        try
        {
            ClearGallery();
            for (var i = 0; i < cards.Length; i++)
            {
                var snapshot = visible[i];
                var card = new Button
                {
                    AutoSize = false, Height = GalleryColorContrast.CardHeightForDpi(gallery.DeviceDpi),
                    Width = GalleryCardWidth(),
                    Margin = new Padding(2, 2, 2, 7),
                    Padding = new Padding(12, 8, 10, 8),
                    FlatStyle = FlatStyle.Flat, TextAlign = ContentAlignment.MiddleLeft,
                    UseVisualStyleBackColor = false, UseMnemonic = false,
                    Text = cards[i], TabStop = true,
                    AccessibleRole = AccessibleRole.PushButton,
                    AccessibleName = SafeAccessibilityText(snapshot.Title),
                    AccessibleDescription = SafeAccessibilityText(snapshot.Summary ?? "")
                };
                StyleGalleryCard(card);
                var url = snapshot.Url;
                card.Click += (_, _) => OpenUrl(url);
                gallery.Controls.Add(card);
            }
        }
        finally { gallery.ResumeLayout(true); }
    }

    private int GalleryCardWidth() => Math.Max(180,
        gallery.ClientSize.Width - gallery.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - 16);

    private void ResizeGalleryCards()
    {
        if (IsDisposed) return;
        var width = GalleryCardWidth();
        var height = GalleryColorContrast.CardHeightForDpi(gallery.DeviceDpi);
        foreach (var card in gallery.Controls.OfType<Button>())
        {
            if (card.Width != width) card.Width = width;
            if (card.Height != height) card.Height = height;
        }
    }

    private Color WidgetAccent()
    {
        var profile = WidgetStudio.Resolve(host.Config.WidgetProfiles, kind);
        return host.HostedByOpsis && profile.AccentMode == "host"
            ? Theme.HostAccent : Theme.Accent;
    }

    private void StyleGalleryCard(Button card)
    {
        card.BackColor = Theme.Surface;
        card.ForeColor = Theme.Text;
        card.FlatAppearance.BorderSize = 1;
        card.FlatAppearance.BorderColor = kind == "breaking" ? WidgetAccent() : Theme.Border;
        card.FlatAppearance.MouseOverBackColor = Theme.Background;
        card.FlatAppearance.MouseDownBackColor = Theme.Background;
    }

    private void ClearGallery()
    {
        foreach (Control child in gallery.Controls.Cast<Control>().ToArray())
        {
            gallery.Controls.Remove(child);
            child.Dispose();
        }
    }

    private static string SafeAccessibilityText(string input)
    {
        var normalized = input.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return normalized.Length > 512 ? normalized[..511] + "…" : normalized;
    }

    private void OpenSelected()
    {
        var index = items.SelectedIndex;
        if (index < 0 || index >= visible.Count) return;
        OpenUrl(visible[index].Url);
    }

    private static void OpenUrl(string? url)
    {
        if (!NetGuard.IsPublicHttpUrl(url)) return;
        try { Process.Start(new ProcessStartInfo(url!) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timer.Stop(); timer.Dispose();
            ClearGallery();
            scaledFont?.Dispose();
            scaledFont = null;
            eventSubscription.Dispose();
            Theme.Changed -= ApplyTheme;
            Theme.HostAccentChanged -= ApplyTheme;
            Loc.Changed -= RefreshSafe;
        }
        base.Dispose(disposing);
    }
}
