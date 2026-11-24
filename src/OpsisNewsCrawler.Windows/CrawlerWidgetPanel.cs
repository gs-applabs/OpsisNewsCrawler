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
    private bool newsVisibilityRegistered;
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
        if (kind == "news") host.NewsWidgetUpdated += RequestRefresh;
        Loc.Changed += RefreshSafe;
        timer.Tick += (_, _) =>
        {
            if (kind == "news") host.NewsWidgetCategoryChanged();
            RefreshSafe();
        };
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
        if (Visible)
        {
            timer.Start();
            UpdateNewsWidgetVisibility();
        }
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        if (newsVisibilityRegistered)
        {
            newsVisibilityRegistered = false;
            host.SetNewsWidgetVisible(false);
        }
        timer.Stop();
        Interlocked.Exchange(ref refreshQueued, 0);
        base.OnHandleDestroyed(e);
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (IsDisposed) return;
        if (Visible && IsHandleCreated) { timer.Start(); RefreshSafe(); }
        else timer.Stop();
        UpdateNewsWidgetVisibility();
    }

    private void UpdateNewsWidgetVisibility()
    {
        if (kind != "news") return;
        var shouldRegister = !IsDisposed && Visible && IsHandleCreated;
        if (newsVisibilityRegistered == shouldRegister) return;
        newsVisibilityRegistered = shouldRegister;
        host.SetNewsWidgetVisible(shouldRegister);
    }

    private void ApplyTheme()
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            if (IsHandleCreated)
                try { BeginInvoke(new Action(ApplyTheme)); }
                catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException) { }
            return;
        }
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
        foreach (Control card in gallery.Controls)
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
        var galleryMode = (profile.Layout == "gallery" && WidgetStudio.CanUseGallery(kind))
            || (profile.Layout == "market-cards" && WidgetStudio.CanUseMarketCards(kind))
            || (profile.Layout == "weather-cards" && WidgetStudio.CanUseWeatherCards(kind));
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
            items.ItemHeight = Math.Max(profile.Layout == "context" ? 26 : 23,
                items.Font.Height + 6);
            heading.Text = WidgetStudio.Title(kind, Loc.IsGreek);
            breakingBanner.Visible = galleryMode && kind == "breaking" && visible.Count > 0;
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
                : SafeAccessibilityText(string.Join(". ", visible.Take(3).Select(x => x.Title)));
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
        var cards = visible.Select(x => kind == "markets"
            ? WidgetStudio.MarketCardText(x.Summary ?? x.Source, x.Title,
                x.PublishedAt, x.Trend, profile, Loc.IsGreek)
            : kind == "weather" ? WidgetStudio.WeatherCardText(x.Title, x.Summary, x.Source,
                x.PublishedAt, profile, Loc.IsGreek)
            : WidgetStudio.GalleryCardText(
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
                var size = GalleryColorContrast.CardHeightForDpi(gallery.DeviceDpi);
                // Quotes and other items without a permitted destination are
                // text cards, not disabled or deceptive clickable buttons.
                var url = snapshot.Url;
                var navigable = NetGuard.IsPublicHttpUrl(url);
                Control card;
                if (navigable)
                {
                    var button = new Button
                    {
                        AutoSize = false, Height = size, Width = GalleryCardWidth(),
                        Margin = new Padding(2, 2, 2, 7), Padding = new Padding(12, 8, 10, 8),
                        FlatStyle = FlatStyle.Flat, TextAlign = ContentAlignment.MiddleLeft,
                        UseVisualStyleBackColor = false, UseMnemonic = false,
                        Text = cards[i], TabStop = true,
                        AccessibleRole = AccessibleRole.PushButton,
                        AccessibleName = SafeAccessibilityText(snapshot.Title),
                        AccessibleDescription = SafeAccessibilityText(snapshot.Summary ?? "")
                    };
                    button.Click += (_, _) => OpenUrl(url);
                    card = button;
                }
                else
                {
                    card = new Label
                    {
                        AutoSize = false, Height = size, Width = GalleryCardWidth(),
                        Margin = new Padding(2, 2, 2, 7), Padding = new Padding(12, 8, 10, 8),
                        TextAlign = ContentAlignment.MiddleLeft,
                        Text = cards[i], TabStop = false, AutoEllipsis = true,
                        AccessibleRole = AccessibleRole.StaticText,
                        AccessibleName = SafeAccessibilityText(snapshot.Title),
                        AccessibleDescription = SafeAccessibilityText(snapshot.Summary ?? "")
                    };
                }
                StyleGalleryCard(card);
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
        foreach (Control card in gallery.Controls)
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

    private void StyleGalleryCard(Control card)
    {
        card.BackColor = Theme.Surface;
        card.ForeColor = Theme.Text;
        if (card is Button button)
        {
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = kind == "breaking" ? WidgetAccent() : Theme.Border;
            button.FlatAppearance.MouseOverBackColor = Theme.Background;
            button.FlatAppearance.MouseDownBackColor = Theme.Background;
        }
        else if (card is Label label)
        {
            label.BorderStyle = BorderStyle.FixedSingle;
        }
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
        // Dispose the owned font only after WinForms has disposed child controls
        // which may still reference the same Font object.
        var releaseFont = disposing ? scaledFont : null;
        if (disposing)
        {
            timer.Stop(); timer.Dispose();
            if (newsVisibilityRegistered)
            {
                newsVisibilityRegistered = false;
                host.SetNewsWidgetVisible(false);
            }
            eventSubscription.Dispose();
            Theme.Changed -= ApplyTheme;
            Theme.HostAccentChanged -= ApplyTheme;
            if (kind == "news") host.NewsWidgetUpdated -= RequestRefresh;
            Loc.Changed -= RefreshSafe;
            ClearGallery();
            scaledFont = null;
        }
        base.Dispose(disposing);
        releaseFont?.Dispose();
    }
}
