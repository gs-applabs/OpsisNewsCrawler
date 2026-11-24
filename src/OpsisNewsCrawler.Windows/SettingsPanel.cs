using OpsisNewsCrawler.Core;
using System.ComponentModel;

namespace OpsisNewsCrawler.Windows;

/// <summary>
/// All crawler settings in one tabbed control, drawn like the Opsis - Desktop Customization UI. The standalone
/// <see cref="SettingsForm"/> hosts it in a window and the Opsis plugin hosts the very same control as the
/// "News Crawler" page of Opsis. Edits are kept in memory and written to the config only when the user presses Apply.
/// Every text is in the one language chosen in <see cref="Loc"/>; the panel rebuilds itself when the language changes.
/// </summary>
public sealed partial class SettingsPanel : UserControl
{
    private readonly TickerHost host;
    private readonly bool showCloseButton;
    private readonly bool showAboutTabs;
    private bool loading;
    private bool hasUnsavedChanges;
    private bool dirtyTrackingAttached;
    private Action? performanceInfoHandler;

    private FlatNav tabs = null!;
    private Label status = null!;
    private bool built;
    private BarPreview barPreview = null!;
    private readonly System.Windows.Forms.Timer previewTimer = new() { Interval = 250 };

    // General
    private FlatComboBox language = null!;
    private FlatComboBox perfMode = null!;
    private FlatComboBox networkCost = null!;
    private Label overviewStatus = null!;
    private readonly System.Windows.Forms.Timer overviewTimer = new() { Interval = 5000 };
    private Label perfInfo = null!;
    private FlatComboBox category = null!;
    private FlatNumber refresh = null!;
    private FlatNumber speed = null!;
    private FlatCheckBox hoverPause = null!;
    private FlatCheckBox startWithWindows = null!;
    private FlatCheckBox toggleHotkey = null!;
    private FlatCheckBox icons = null!;
    private FlatCheckBox preview = null!;
    private FlatCheckBox pauseOther = null!;
    private FlatCheckBox hideRead = null!;

    // Bars
    private FlatComboBox defaultBar = null!;
    private SlotPicker mainPicker = null!;
    private SlotPicker secondPicker = null!;
    private SlotPicker miniPicker = null!;
    private readonly Dictionary<string, FlatCheckBox> weatherFieldChecks = new();
    private FlatCheckBox secondEnabled = null!;
    private readonly List<(Screen Screen, FlatCheckBox Check, SlotPicker Picker)> monitorRows = new();
    private FlatCheckBox autoUpdate = null!;
    private FlatText citySearch = null!;
    private FlatList cityList = null!;
    private FlatCheckBox miniWeather = null!;
    private FlatNumber miniX = null!, miniY = null!, miniW = null!, miniH = null!;
    private FlatButton miniMove = null!;
    private FlatCheckBox miniOnTop = null!;
    private FlatCheckBox miniWhenHidden = null!;
    private FlatCheckBox yahooHistory = null!;
    private FlatText yahooSearch = null!;
    private FlatButton yahooSearchButton = null!, yahooAddButton = null!, yahooRemoveButton = null!;
    private FlatList yahooHitList = null!;
    private FlatComboBox yahooGroup = null!;
    private Label yahooStatus = null!;
    private readonly List<FlatButton> yahooPresetButtons = new();
    private List<YahooHit> yahooHits = new();
    private Control yahooCatalog = null!;
    private FlatComboBox miniShape = null!;
    private FlatComboBox barShape = null!;
    private List<string> workCities = new();
    private List<GreekCity> workCustom = new();
    private FlatText geoQuery = null!;
    private FlatButton geoSearchButton = null!, geoAddButton = null!, geoCleanupButton = null!, geoMyLocationButton = null!;
    private FlatList geoList = null!;
    private Label geoStatus = null!;
    private List<GeoPlace> geoHits = new();
    private FlatComboBox hazardRegion = null!, hazardMinMagnitude = null!;
    private FlatNumber hazardMaxAge = null!, hazardRefresh = null!;
    private FlatCheckBox hazardInIcon = null!, weatherInIcon = null!;
    private readonly Dictionary<HazardKind, FlatCheckBox> hazardKindChecks = new();
    private readonly Dictionary<HazardSources, FlatCheckBox> hazardSourceChecks = new();
    private static readonly double[] MagnitudeChoices = { 2.5, 3.0, 3.5, 4.0, 4.5, 5.0, 5.5, 6.0, 6.5, 7.0, 7.5 };
    private List<GreekCity> shownCities = new();
    private FlatCheckBox barsEnabled = null!;
    private Label updateStatus = null!;
    private FlatButton installUpdate = null!;
    private FlatButton openUpdatePage = null!;
    private FlatProgress updateProgress = null!;
    private FlatList laterList = null!;
    private Label laterStatus = null!;
    private List<SavedArticle> laterShown = new();
    private Label secondNote = null!;

    // Position
    private FlatComboBox position = null!;
    private FlatCheckBox followTaskbar = null!;
    private FlatCheckBox reserve = null!;
    private FlatNumber posX = null!;
    private FlatNumber posY = null!;
    private FlatNumber posW = null!;
    private FlatComboBox monitor = null!;
    private FlatButton moveWithMouse = null!;
    private FlatCheckBox tbAuto = null!;
    private FlatNumber tbLeft = null!;
    private FlatNumber tbRight = null!;
    private FlatCard customCard = null!;
    private FlatCard taskbarCard = null!;

    // Sources
    private List<FeedCategory> workCategories = new();
    private FlatList categoryList = null!;
    private FlatList feedList = null!;
    private FlatText siteBox = null!;
    private FlatComboBox presets = null!;
    private Label discoverStatus = null!;
    private FlatNumber newsMaxAge = null!;
    private FlatCheckBox newsNewestFirst = null!;
    private FlatCheckBox stockNewsOnly = null!;
    private FlatText mutedNewsKeywords = null!;

    // Markets
    private List<QuoteInstrument> workWatch = new();
    private List<PriceAlert> workAlerts = new();
    private List<QuoteInstrument> alertChoices = new();
    private string alertChoicesSignature = "";
    private FlatList alertList = null!;
    private FlatComboBox alertInstrument = null!;
    private FlatComboBox alertDirection = null!;
    private FlatComboBox alertMetric = null!;
    private FlatText alertThreshold = null!;
    private readonly Dictionary<QuoteSource, FlatList> watchLists = new();
    private FlatComboBox marketCombo = null!;
    private FlatText stockSearch = null!;
    private FlatList stockList = null!;
    private FlatButton refreshCatalog = null!;
    private Label stockStatus = null!;
    private Label sourceStatus = null!;
    private readonly System.Windows.Forms.Timer sourceTimer = new() { Interval = 3000 };
    private List<QuoteInstrument> visibleStocks = new();
    private readonly Dictionary<string, IReadOnlyList<QuoteInstrument>> catalogs = new();
    private string SelectedMic => EuronextCatalog.Markets[Math.Clamp(marketCombo.SelectedIndex, 0, EuronextCatalog.Markets.Count - 1)].Mic;

    // Look
    private FlatComboBox themeMode = null!;
    private FlatCheckBox themeAccent = null!;
    private FlatNumber speedMainB = null!, speedSecond = null!, speedSecondB = null!, speedMini = null!, speedMiniB = null!, speedMonitor = null!, speedMonitorB = null!;
    private Control themeRow = null!;

    private static int ThemeIndex(string mode) => mode switch { "light" => 1, "dark" => 2, "host" => 3 + Theme.NamedThemes.Length, _ => Array.IndexOf(Theme.NamedThemes, mode) is >= 0 and var i ? 3 + i : 0 };
    private static string ThemeNameAt(int index) => index switch { 1 => "light", 2 => "dark", _ when index == 3 + Theme.NamedThemes.Length => "host", >= 3 when index - 3 < Theme.NamedThemes.Length => Theme.NamedThemes[index - 3], _ => "system" };
    private FlatSlider opacity = null!;
    private Label opacityLabel = null!;
    private FlatCheckBox edgeFade = null!;
    private FlatSlider windowOpacity = null!;
    private Label windowOpacityLabel = null!;
    private Control windowOpacityRow = null!;
    private Label themeNote = null!;
    private FlatComboBox barColor = null!;
    private FlatSwatch barSwatch = null!;
    private FlatComboBox taskbarColor = null!;
    private FlatSwatch taskbarSwatch = null!;
    private FlatComboBox barBackdrop = null!;
    private FlatComboBox taskbarBackdrop = null!;

    // P1: edit existing Opsis desktop widgets, never register duplicate widget keys.
    private readonly Dictionary<string, (FlatComboBox Layout, FlatNumber Count, FlatCheckBox Source, FlatCheckBox Time)> widgetEditors = new();

    // Breaking
    private FlatCheckBox breakingEnabled = null!;
    private FlatCheckBox breakingSound = null!;
    private FlatCheckBox weatherMapOnClick = null!;
    private FlatCheckBox weatherAlerts = null!;
    private FlatComboBox alertCity = null!;
    private FlatText keywords = null!;
    private FlatComboBox worldContinent = null!, worldCountry = null!;
    private FlatList worldList = null!;
    private WorldCountry? shownCountry;
    private bool refreshingWorld;
    private FlatComboBox worldFeedContinent = null!, worldFeedCountry = null!;
    private FlatList worldFeedList = null!;
    private FlatComboBox mainFont = null!, secondFont = null!, miniFont = null!, monitorFont = null!;
    private FlatNumber mainFontPct = null!, secondFontPct = null!, miniFontPct = null!, monitorFontPct = null!;
    private FlatToggle mainHeightAuto = null!, monitorHeightAuto = null!;
    private FlatNumber mainHeight = null!, monitorHeight = null!;
    private List<string> fontNames = new();
    private FlatComboBox textColorMode = null!;
    private FlatToggle textShadow = null!;
    private FlatList historyList = null!;
    private FlatList historyPreview = null!;
    private Label historyStatus = null!;
    private Label historyUsed = null!;
    private Label historyNone = null!;
    private FlatCard historyPreviewCard = null!;
    private FlatButton historyAddToBar = null!;
    private List<string> workDisabledFeeds = new();
    private FlatNumber feedMaxLines = null!;
    private FlatComboBox feedOrder = null!, feedCountry = null!;
    private List<string> feedCountryCodes = new() { "" };
    private List<OpsisNewsCrawler.Plugin.Abstractions.PluginFeed> historyFeeds = new();
    private bool refreshingHistory;
    private readonly System.Windows.Forms.Timer historyRetry = new() { Interval = 3000 };
    private int historyRetries;
    private FlatComboBox breakingSensitivity = null!;
    private FlatButton forgetBreaking = null!;
    private FlatNumber maxAge = null!;
    private FlatNumber duration = null!;
    private List<LiveFeed> workLive = new();
    private FlatList liveList = null!;
    private FlatText addLiveBox = null!;
    private FlatNumber livePriority = null!;
    private FlatText liveRegion = null!, liveKeywords = null!;
    private bool liveMetadataLoading;
    private Label liveStatus = null!;

    /// <summary>Raised after Apply succeeded.</summary>
    public event Action? Applied;

    /// <summary>Raised when the user presses the close button (the window hides itself).</summary>

    /// <summary>True when Opsis pushes its own colours and language; then those two choices are locked.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool HostThemeLocked { get; set; }

    public SettingsPanel(TickerHost host, bool showCloseButton, bool showAboutTabs = true)
    {
        this.host = host;
        this.showCloseButton = showCloseButton;
        this.showAboutTabs = showAboutTabs;
        Dock = DockStyle.Fill;
        RightToLeft = Loc.IsRtl ? RightToLeft.Yes : RightToLeft.No;
        Font = new Font("Segoe UI", 9.5f);
        DoubleBuffered = true;

        host.CustomBoundsChanged += OnCustomBoundsChanged;
        host.ReadLater.Changed += OnReadLaterChanged;
        host.BarsEnabledChanged += OnBarsEnabledChanged;
        host.MiniWeatherBoundsChanged += OnMiniBoundsChanged;
        Theme.Changed += OnThemeChanged;
        Loc.Changed += OnLanguageChanged;
        previewTimer.Tick += (_, _) => UpdatePreview();
        Build();
        overviewTimer.Tick += (_, _) => RefreshOverview();
        if (Visible && IsHandleCreated) { previewTimer.Start(); overviewTimer.Start(); }
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (IsDisposed) return;
        if (Visible)
        {
            previewTimer.Start();
            overviewTimer.Start();
            if (sourceStatus is { IsDisposed: false }) sourceTimer.Start();
        }
        else
        {
            previewTimer.Stop();
            overviewTimer.Stop();
            sourceTimer.Stop();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            previewTimer.Stop();
            if (performanceInfoHandler is not null) host.PerformanceChanged -= performanceInfoHandler;
            overviewTimer.Stop();
            overviewTimer.Dispose();
            previewTimer.Dispose();
            sourceTimer.Dispose();
            historyRetry.Dispose();
            OpsisNewsCrawler.Plugin.Abstractions.PluginFeeds.Changed -= OnPluginFeedsChanged;
            host.CustomBoundsChanged -= OnCustomBoundsChanged;
            host.ReadLater.Changed -= OnReadLaterChanged;
            host.BarsEnabledChanged -= OnBarsEnabledChanged;
            host.MiniWeatherBoundsChanged -= OnMiniBoundsChanged;
            Theme.Changed -= OnThemeChanged;
            Loc.Changed -= OnLanguageChanged;
        }
        base.Dispose(disposing);
    }

    private void RefreshOverview()
    {
        if (IsDisposed || overviewStatus is null || overviewStatus.IsDisposed) return;
        var sources = host.FeedHealth;
        var ok = sources.Count(h => h.ConsecutiveFailures == 0 && h.LastOk is not null);
        var disk = DataCache.WriteDiagnostics;
        overviewStatus.Text = Loc.T("dash.metrics",
            host.RecentNews.Count, ok, sources.Count, host.Performance.Tier, host.RunningRefreshes,
            disk.Writes, disk.Skips, host.CurrentEnergyBudget.Multiplier);
    }

    private void OnThemeChanged()
    {
        if (IsDisposed) return;
        Theme.Apply(this);
        Invalidate(true);
    }

    private void OnLanguageChanged()
    {
        if (IsDisposed) return;
        if (InvokeRequired) BeginInvoke(new Action(Rebuild));
        else Rebuild();
    }

    private static string L(string key) => Loc.T(key);

    // ---- preview of the bars ----------------------------------------------------------------

    /// <summary>Sub-pages of the Settings tab whose choices change how a bar looks: Bars, Position, Look, Breaking.</summary>
    private static readonly int[] PreviewSubs = { 1, 2, 3 };

    /// <summary>The desktop bar's place and size as fractions of the primary screen (for the preview).</summary>
    private RectangleF DesktopRelative()
    {
        var b = (Screen.PrimaryScreen ?? Screen.AllScreens[0]).Bounds;
        return new RectangleF((miniX.Value - b.Left) / (float)b.Width, (miniY.Value - b.Top) / (float)b.Height, miniW.Value / (float)b.Width, miniH.Value / (float)b.Height);
    }

    private void UpdatePreview()
    {
        if (!Visible || !built || barPreview is null || barPreview.IsDisposed || tabs is null) return;
        var show = PreviewSubs.Contains(tabs.SelectedIndex);
        if (barPreview.Visible != show) barPreview.Visible = show;
        if (!show) return;
        var dark = themeMode.SelectedIndex switch { 1 => false, 2 => true, 3 or 4 or 6 => true, 5 => false, _ => Theme.IsDark };
        barPreview.SetState(new BarPreviewState(
            (TickerPosition)Math.Max(0, position.SelectedIndex), mainPicker.Get(),
            secondEnabled.Checked, secondPicker.Get(),
            dark, opacity.Value, edgeFade.Checked,
            barColor.SelectedIndex switch { 1 => "lightblue", 2 => "accent", 3 => "custom", _ => "theme" }, barSwatch.Rgb,
            taskbarColor.SelectedIndex switch { 1 => "lightblue", 2 => "accent", 3 => "custom", _ => "taskbar" }, taskbarSwatch.Rgb,
            barBackdrop.SelectedIndex switch { 1 => "blur", 2 => "gradient", _ => "acrylic" },
            taskbarBackdrop.SelectedIndex switch { 0 => "acrylic", 2 => "gradient", _ => "blur" },
            miniWeather.Checked, miniPicker.Get(), DesktopRelative(), miniShape.SelectedIndex switch { 1 => "rounded", 2 => "pill", _ => "rect" },
            barShape.SelectedIndex switch { 1 => "rounded", 2 => "pill", _ => "rect" }));
    }

    // ---- building ------------------------------------------------------------------------

    private void Rebuild()
    {
        RightToLeft = Loc.IsRtl ? RightToLeft.Yes : RightToLeft.No;
        var selectedTab = built ? CurrentLeaf() : 0;
        SuspendLayout();
        foreach (var c in Controls.Cast<Control>().ToList()) c.Dispose();
        Controls.Clear();
        dirtyTrackingAttached = false;
        Build();
        SelectTab(selectedTab);
        ResumeLayout(true);
    }

    private void Build()
    {
        loading = true;
        watchLists.Clear();
        tabs = new FlatNav { Dock = DockStyle.Fill };
        tabs.Badge = new AnimatedAppIcon(() => !MinimiseAnimation.Simple);   // always moving: the tray option is for the tray icon only

        // One navigation rail: three groups (Settings, Sources, Information), every page under its group.
        AddGroup(Loc.T("pk.settings"),
            (Loc.T("pk.general"), BuildGeneral()),
            // Every bar setting is on one page: placement, what each bar shows, the desktop bar.
            (L("tab.bars"), Combine(
                (Loc.T("pk.placement.and.behaviour.where"), BuildPosition()),
                (Loc.T("pk.what.each.bar.shows"), BuildBars()),
                (Loc.T("pk.desktop.bar.a.free"), BuildDesktopBar()),
                (Loc.T("pk.other.monitors.one.bar"), BuildMonitors()))),
            (L("tab.look"), BuildLook()),
            (Loc.IsGreek ? "P1 · Στούντιο widgets" : "P1 · Widget Studio", BuildWidgetStudio()),
            (L("tab.breaking"), BuildBreaking()));
        AddGroup(L("tab.sources"),
            (Loc.T("pk.news.flow"), Combine((null, BuildSources()), (L("tab.readLater"), BuildReadLater()))),
            (Loc.T("pk.markets.flow"), BuildMarkets()),
            (Loc.T("pk.weather.flow"), BuildWeather()),
            (L("hazard.flow"), BuildHazards()),
            (L("history.flow"), BuildHistory()),
            (Loc.IsGreek ? "P2 · Έξυπνες ειδήσεις" : "P2 · News intelligence", BuildP2Insights()));
        var info = new List<(string, Control)>();
        if (showAboutTabs)
        {
            info.Add((L("tab.help"), BuildInfoPage(() => AboutTexts.Help, host)));
            info.Add((L("tab.instructions"), BuildInfoPage(() => AboutTexts.Instructions, host)));
            info.Add((L("tab.history"), BuildInfoPage(() => AboutTexts.History)));
            info.Add((L("tab.license"), BuildInfoPage(() => AboutTexts.License)));
        }
        info.Add((L("tab.updates"), BuildUpdates()));
        info.Add((L("tab.dependencies"), BuildDependencies()));
        AddGroup(Loc.T("pk.information"), info.ToArray());

        // Every change is saved by itself a moment after it is made (see AutoApply): there is no Apply button, and the window closes with its own X.
        // Export / import of the settings live on the General page (card "Backup of the settings").
        status = new Label { AutoSize = true, MaximumSize = new Size(900, 0), Padding = new Padding(8, 11, 0, 0), Tag = "muted", Text = L("settings.legend") };
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Padding = new Padding(12, 4, 12, 6) };
        bar.Controls.Add(status);
        autoApplyTimer.Tick += (_, _) =>
        {
            autoApplyTimer.Stop();
            if (!IsDisposed && !loading && built && hasUnsavedChanges) ApplySettings();
        };
        VisibleChanged += (_, _) => FlushPendingChanges();   // leaving the page (Opsis) or hiding the window saves what is pending

        barPreview = new BarPreview { Dock = DockStyle.Bottom, Visible = false };
        Controls.Add(tabs);
        Controls.Add(barPreview);
        Controls.Add(bar);
        built = true;

        DescribeToggles();
        EnableCardResets();
        LoadFromConfig();
        AttachDirtyTracking();
        Theme.Apply(this);
        loading = false;
        hasUnsavedChanges = false;
    }

    /// <summary>A group heading of the navigation rail with its pages; every page is one of the Build... pages.</summary>
    private void AddGroup(string title, params (string Title, Control Page)[] pages)
    {
        tabs.AddGroup(title);
        foreach (var (pageTitle, page) in pages) tabs.AddPage(pageTitle, page);
    }

    private int CurrentLeaf() => Math.Max(0, tabs.SelectedIndex);

    /// <summary>Puts the content of several pages one under the other on a single scrolling page.</summary>
    private static Panel Combine(params (string? Heading, Control Page)[] parts)
    {
        var page = new StableScrollPanel { Padding = new Padding(8, 6, 8, 6) };
        var stack = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        foreach (var (heading, part) in parts)
        {
            if (heading is not null)
            {
                // "Title|description": a section heading with a muted line under it
                var split = heading.Split('|', 2);
                stack.Controls.Add(new Label { Text = split[0], AutoSize = true, Font = new Font("Segoe UI Semibold", 13f), Margin = new Padding(0, stack.Controls.Count == 0 ? 6 : 26, 0, 0) });
                if (split.Length > 1)
                    stack.Controls.Add(new Label { Text = split[1], AutoSize = true, MaximumSize = new Size(FlatCard.CardWidth, 0), Tag = "muted", Margin = new Padding(0, 2, 0, 4) });
            }
            foreach (var child in part.Controls.Cast<Control>().ToList())
            {
                part.Controls.Remove(child);
                child.Dock = DockStyle.None;
                stack.Controls.Add(child);
            }
            part.Dispose();
        }
        page.Controls.Add(stack);
        return page;
    }

    private static Panel NewPage(out FlowLayoutPanel flow)
    {
        var page = new StableScrollPanel { Padding = new Padding(8, 6, 8, 6) };
        flow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        page.Controls.Add(flow);
        return page;
    }

    /// <summary>"Label ...... control" row.</summary>
    private static Control Row(string label, Control control, int labelWidth = 300)
    {
        var row = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 2, 0, 2) };
        // The label grows to a second or third line instead of being cut off (long translations), so the row adapts to its content.
        var needed = TextRenderer.MeasureText(label, new Font("Segoe UI", 9f), new Size(labelWidth, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + 10;
        row.Controls.Add(new Label { Text = label, AutoSize = false, Width = labelWidth, Height = Math.Max(32, needed), TextAlign = ContentAlignment.MiddleLeft });
        row.Controls.Add(control);
        return row;
    }

    private static Label Note(string text, int width = 640) =>
        new() { Text = text, AutoSize = true, MaximumSize = new Size(width, 0), Tag = "muted", Margin = new Padding(0, 4, 0, 8) };

    private Control BuildGeneral()
    {
        var page = NewPage(out var flow);

        language = new FlatComboBox { Width = 260 };
        var langs = Loc.Available();
        language.SetItems(langs.Select(l => l.Native), 0);
        language.SelectedIndexChanged += (_, _) =>
        {
            if (loading || language.SelectedIndex < 0) return;
            // The language is applied at once (the panel rebuilds in the new language); it is saved with Apply.
            host.Config.Language = langs[language.SelectedIndex].Code;
            if (!host.LanguageFollowsHost) Loc.SetLanguage(host.Config.Language);
        };
        barsEnabled = new FlatToggle { RowWidth = 600, Text = L("chk.barsEnabled") };
        flow.Controls.Add(barsEnabled);
        startWithWindows = new FlatToggle { RowWidth = 600, Text = Loc.T("pk.start.with.windows"), Visible = !host.HostedByOpsis };
        flow.Controls.Add(startWithWindows);
        toggleHotkey = new FlatToggle { RowWidth = 600, Text = Loc.T("pk.hot.key.ctrl.alt"), Visible = !host.HostedByOpsis };
        flow.Controls.Add(toggleHotkey);
        var languageRow = Row(L("lbl.language"), language);
        languageRow.Visible = !host.LanguageFollowsHost;
        flow.Controls.Add(languageRow);

        category = new FlatComboBox { Width = 320 };
        flow.Controls.Add(Row(L("lbl.category"), category));
        refresh = new FlatNumber();
        refresh.Configure(AppConfig.MinRefreshSeconds, AppConfig.MaxRefreshSeconds, 30);
        flow.Controls.Add(Row(L("lbl.refresh"), refresh));
        speed = new FlatNumber();
        speed.Configure(AppConfig.MinScrollSpeed, AppConfig.MaxScrollSpeed, 10);
        flow.Controls.Add(Row(L("lbl.speed"), speed));

        // Each bar, and each half of a split bar, has its own speed; 0 = the same as the speed above.
        var speedCard = new FlatCard(L("speed.card"));
        var speedFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        speedFlow.Controls.Add(Note(L("speed.note"), 700));
        FlatNumber SpeedBox()
        {
            var box = new FlatNumber { Width = 120 };
            box.Configure(0, AppConfig.MaxScrollSpeed, 10);
            return box;
        }
        speedMainB = SpeedBox(); speedSecond = SpeedBox(); speedSecondB = SpeedBox(); speedMini = SpeedBox(); speedMiniB = SpeedBox(); speedMonitor = SpeedBox(); speedMonitorB = SpeedBox();
        speedFlow.Controls.Add(Row(L("speed.mainB"), speedMainB, 340));
        speedFlow.Controls.Add(Row(L("speed.second"), speedSecond, 340));
        speedFlow.Controls.Add(Row(L("speed.secondB"), speedSecondB, 340));
        speedFlow.Controls.Add(Row(L("speed.mini"), speedMini, 340));
        speedFlow.Controls.Add(Row(L("speed.miniB"), speedMiniB, 340));
        speedFlow.Controls.Add(Row(L("speed.monitors"), speedMonitor, 340));
        speedFlow.Controls.Add(Row(L("speed.monitorsB"), speedMonitorB, 340));
        speedCard.Controls.Add(speedFlow);
        flow.Controls.Add(speedCard);

        // Performance: Automatic follows the PC (what was detected is shown); the others force a tier.
        var perfCard = new FlatCard(L("card.performance"));
        var perfFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        perfMode = new FlatComboBox { Width = 260 };
        perfMode.SetItems(new[] { L("perf.auto"), L("perf.low"), L("perf.balanced"), L("perf.high") }, 0);
        perfFlow.Controls.Add(Row(L("perf.mode"), perfMode, 300));
        perfFlow.Controls.Add(Note(L("perf.note"), 700));
        perfInfo = new Label { AutoSize = true, MaximumSize = new Size(700, 0), Tag = "muted", Margin = new Padding(0, 2, 0, 6), UseMnemonic = false };
        perfFlow.Controls.Add(perfInfo);
        perfCard.Controls.Add(perfFlow);
        flow.Controls.Add(perfCard);

        // P1 energy budget: explicit metered mode; no unreliable automatic billing inference.
        networkCost = new FlatComboBox { Width = 260 };
        networkCost.SetItems(new[] { L("energy.normal"), L("energy.metered") }, 0);
        flow.Controls.Add(Row(L("energy.label"), networkCost, 300));

        // Shared Opsis.UI FlatCard styling; does not replace existing navigation or introduce new UI conventions.
        var dashboardCard = new FlatCard(L("dash.title"));
        var dashboardFlow = new FlowLayoutPanel
        {
            AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false
        };
        overviewStatus = new Label { AutoSize = true, MaximumSize = new Size(770, 0), Tag = "muted", UseMnemonic = false };
        dashboardFlow.Controls.Add(overviewStatus);
        var details = new FlatButton { Text = L("diag.button") };
        details.Click += (_, _) => { using var dialog = new P0DiagnosticsForm(host); dialog.ShowDialog(this); };
        dashboardFlow.Controls.Add(details);
        dashboardCard.Controls.Add(dashboardFlow);
        flow.Controls.Add(dashboardCard);
        RefreshOverview();
        if (performanceInfoHandler is not null) host.PerformanceChanged -= performanceInfoHandler;
        performanceInfoHandler = () =>
        {
            if (!IsDisposed && IsHandleCreated)
            {
                try { BeginInvoke(new Action(UpdatePerformanceInfo)); }
                catch (InvalidOperationException) { }
            }
        };
        host.PerformanceChanged += performanceInfoHandler;

        var backupCard = new FlatCard(L("card.backup"));
        var backupFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
        var exportButton = new FlatButton { Text = L("settings.export") };
        exportButton.Click += (_, _) => ExportSettings();
        var importButton = new FlatButton { Text = L("settings.import") };
        importButton.Click += (_, _) => ImportSettings();
        backupFlow.Controls.Add(exportButton);
        backupFlow.Controls.Add(importButton);
        backupCard.Controls.Add(backupFlow);
        flow.Controls.Add(backupCard);

        hoverPause = new FlatToggle { RowWidth = 600, Text = L("chk.hoverPause") };
        icons = new FlatToggle { RowWidth = 600, Text = L("chk.icons") };
        preview = new FlatToggle { RowWidth = 600, Text = L("chk.preview") };
        pauseOther = new FlatToggle { RowWidth = 600, Text = L("chk.pauseOther") };
        hideRead = new FlatToggle { RowWidth = 600, Text = L("chk.hideRead") };
        foreach (var c in new Control[] { hoverPause, icons, preview, pauseOther, hideRead }) flow.Controls.Add(c);

        var reset = new FlatButton { Text = L("btn.resetRead") };
        reset.Margin = new Padding(0, 10, 0, 3);
        var openLog = new FlatButton { Text = L("btn.openLog") };
        openLog.Margin = new Padding(0, 3, 0, 3);
        openLog.Click += (_, _) => OpenLog();
        reset.Click += (_, _) =>
        {
            host.ResetReadList();
            SetStatus(L("msg.readCleared"));
        };
        flow.Controls.Add(reset);
        flow.Controls.Add(openLog);
        var diagnostics = new FlatButton
        {
            Text = Loc.T("diag.button")
        };
        diagnostics.Click += (_, _) =>
        {
            using var dialog = new P0DiagnosticsForm(host);
            dialog.ShowDialog(this);
        };
        flow.Controls.Add(diagnostics);
        return page;
    }

    /// <summary>The update frame (Information tab).</summary>
    // ---- dependencies ----------------------------------------------------------------------------

    private Label webViewStatus = null!;
    private FlatCheckBox webViewCheck = null!;
    private FlatButton webViewInstall = null!;

    private Control BuildDependencies()
    {
        var page = NewPage(out var flow);
        flow.Controls.Add(Note(L("note.dependencies"), 700));
        var card = new FlatCard(L("tab.dependencies"));
        var inner = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };

        // .NET: the program is running, so it is there (shown ticked, nothing to choose).
        var dotNet = new FlatCheckBox { Text = L("dep.dotnet") + "  —  " + Dependencies.DotNetVersion + "  (" + L("dep.installed") + ")", Checked = true, Enabled = false };
        inner.Controls.Add(dotNet);

        // Present: ticked and locked, like .NET. Missing: unticked, with an install button.
        webViewCheck = new FlatCheckBox { Enabled = false };
        webViewStatus = new Label { AutoSize = true, Margin = new Padding(0, 4, 0, 0), Tag = "muted" };
        inner.Controls.Add(webViewCheck);
        webViewInstall = new FlatButton { Text = L("btn.installDep"), Margin = new Padding(0, 4, 0, 0) };
        inner.Controls.Add(webViewStatus);
        inner.Controls.Add(webViewInstall);
        webViewInstall.Click += async (_, _) =>
        {
            webViewInstall.Enabled = false;
            webViewStatus.Text = L("msg.depInstalling");
            var result = await Dependencies.InstallWebView2Async();
            if (IsDisposed) return;
            webViewInstall.Enabled = true;
            RefreshDependencies();
            if (result != Dependencies.InstallResult.Installed)
                webViewStatus.Text += "  —  " + L(result == Dependencies.InstallResult.NotSignedByMicrosoft ? "msg.depNotSigned" : "msg.depFailed");
        };
        card.Controls.Add(inner);
        flow.Controls.Add(card);
        RefreshDependencies();
        return page;
    }

    private void RefreshDependencies()
    {
        if (webViewStatus is null || webViewStatus.IsDisposed) return;
        var version = Dependencies.WebView2Version();
        webViewCheck.Text = L("dep.webview2") + "  —  " + (version is null ? L("dep.missing") : version + "  (" + L("dep.installed") + ")");
        webViewCheck.Checked = version is not null;
        webViewStatus.Text = "";
        webViewInstall.Visible = version is null; // installed: shown, nothing to choose
    }

    private Control BuildUpdates()
    {
        var page = NewPage(out var flow);
        // Same logic as GearWin: look at every start (and every 12 hours), tell once per version, install only after a click and a confirmation,
        // from this repository's GitHub releases only, verified with SHA-256, silently after the program has ended, then open it again.
        var updates = new FlatCard(L("card.updates"));
        var updFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        updFlow.Controls.Add(Note(L("note.updates"), 700));
        autoUpdate = new FlatToggle { RowWidth = 600, Text = L("chk.autoUpdate") };
        updFlow.Controls.Add(autoUpdate);
        var checkNow = new FlatButton { Text = L("btn.checkNow") };
        installUpdate = new FlatButton { Text = L("btn.installUpdate"), Visible = false };
        installUpdate.SetPrimary(true);
        openUpdatePage = new FlatButton { Text = L("btn.openPage"), Visible = false };
        var updButtons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 4, 0, 0) };
        updButtons.Controls.Add(checkNow);
        updButtons.Controls.Add(installUpdate);
        updButtons.Controls.Add(openUpdatePage);
        updFlow.Controls.Add(updButtons);
        updateProgress = new FlatProgress { Visible = false, Width = 420 };
        updFlow.Controls.Add(updateProgress);
        updateStatus = new Label { AutoSize = true, Tag = "muted", MaximumSize = new Size(700, 0), Margin = new Padding(0, 6, 0, 0), UseMnemonic = false, Text = Loc.T("upd.current", AboutTexts.Version) };
        updFlow.Controls.Add(updateStatus);
        updates.Controls.Add(updFlow);
        flow.Controls.Add(updates);
        checkNow.Click += async (_, _) => await CheckUpdatesNowAsync(checkNow);
        installUpdate.Click += async (_, _) => await InstallUpdateAsync();
        openUpdatePage.Click += (_, _) => { if (host.AvailableUpdate is { } u) OpenInBrowser(u.PageUrl); };
        ShowUpdateState();
        return page;
    }

    private Control BuildBars()
    {
        var page = NewPage(out var flow);

        var main = new FlatCard(L("card.mainBar"));
        var mainFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        mainPicker = new SlotPicker();
        mainFlow.Controls.Add(mainPicker.Build());
        mainFlow.Controls.Add(Note(SplitNote(), 600));
        main.Controls.Add(mainFlow);
        flow.Controls.Add(main);


        var second = new FlatCard(L("card.secondBar"));
        var secondFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        secondEnabled = new FlatToggle { RowWidth = 600, Text = L("chk.secondEnabled") };
        secondFlow.Controls.Add(secondEnabled);
        secondPicker = new SlotPicker();
        secondFlow.Controls.Add(secondPicker.Build());
        secondNote = Note(L("note.secondBar"), 560);
        secondFlow.Controls.Add(secondNote);
        second.Controls.Add(secondFlow);
        flow.Controls.Add(second);

        secondEnabled.CheckedChanged += (_, _) => UpdateBarsUi();
        return page;
    }

    /// <summary>The bars of the other monitors (one per screen, each with its own content): the last section of the Bars page.</summary>
    private Control BuildMonitors()
    {
        var page = NewPage(out var flow);
        // One bar on every other monitor, each with its own content.
        var monitors = new FlatCard(L("card.monitors"));
        var monitorFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        monitorRows.Clear();
        var others = Screen.AllScreens.Where(s => !s.Primary).ToList();
        for (var i = 0; i < others.Count; i++)
        {
            var s = others[i];
            var check = new FlatCheckBox { Text = Loc.T("fmt.monitor", Array.IndexOf(Screen.AllScreens, s) + 1, s.Bounds.Width, s.Bounds.Height) };
            var picker = new SlotPicker();
            monitorFlow.Controls.Add(check);
            monitorFlow.Controls.Add(picker.Build());
            monitorRows.Add((s, check, picker));
        }
        monitorFlow.Controls.Add(Note(others.Count > 0 ? L("note.monitors") : L("note.noOtherMonitor"), 560));
        monitors.Controls.Add(monitorFlow);
        flow.Controls.Add(monitors);
        return page;
    }

    private Control BuildPosition()
    {
        var page = NewPage(out var flow);

        var def = new FlatCard(L("card.defaultBar"));
        var defFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        defaultBar = new FlatComboBox { Width = 470 };
        defaultBar.SetItems(new[] { L("defaultbar.above"), L("defaultbar.inside"), L("defaultbar.other") }, 0);
        defFlow.Controls.Add(Row(L("lbl.defaultBar"), defaultBar, 200));
        defFlow.Controls.Add(Note(L("note.defaultBar"), 600));
        def.Controls.Add(defFlow);
        flow.Controls.Add(def);
        defaultBar.SelectedIndexChanged += (_, _) =>
        {
            if (loading) return;
            if (defaultBar.SelectedIndex == 0) position.SelectedIndex = (int)TickerPosition.BottomTaskbar;
            else if (defaultBar.SelectedIndex == 1) position.SelectedIndex = (int)TickerPosition.TaskbarIntegrated;
        };


        position = new FlatComboBox { Width = 470 };
        position.SetItems(new[] { L("pos.bottom"), L("pos.top"), L("pos.center"), L("pos.custom"), L("pos.taskbar") }, 0);
        followTaskbar = new FlatToggle { RowWidth = 600, Text = L("chk.followTaskbar") };
        reserve = new FlatToggle { RowWidth = 600, Text = L("chk.reserve") };
        var placement = new FlatCard(Loc.T("pk.exact.placement.and.behaviour"));
        var placementFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        placementFlow.Controls.Add(Row(L("lbl.position"), position, 200));
        barShape = new FlatComboBox { Width = 470 };
        barShape.SetItems(new[] { L("shape.rect"), L("shape.rounded"), L("shape.pill") }, 0);
        placementFlow.Controls.Add(Row(L("lbl.barShape"), barShape, 270));
        placementFlow.Controls.Add(followTaskbar);
        placementFlow.Controls.Add(reserve);
        placement.Controls.Add(placementFlow);
        flow.Controls.Add(placement);

        customCard = new FlatCard(L("card.custom"));
        var cf = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        posX = new FlatNumber();
        posX.Configure(-20000, 20000, 10);
        posY = new FlatNumber();
        posY.Configure(-20000, 20000, 10);
        posW = new FlatNumber();
        posW.Configure(200, 20000, 50);
        monitor = new FlatComboBox { Width = 340 };
        monitor.SetItems(Screen.AllScreens.Select((s, i) => L("fmt.monitor").Replace("{0}", (i + 1).ToString()).Replace("{1}", s.Bounds.Width.ToString()).Replace("{2}", s.Bounds.Height.ToString())), -1);
        cf.Controls.Add(Row(L("lbl.x"), posX, 240));
        cf.Controls.Add(Row(L("lbl.y"), posY, 240));
        cf.Controls.Add(Row(L("lbl.width"), posW, 240));
        cf.Controls.Add(Row(L("lbl.monitor"), monitor, 240));
        moveWithMouse = new FlatButton { Text = L("btn.moveMouse") };
        cf.Controls.Add(moveWithMouse);
        customCard.Controls.Add(cf);
        flow.Controls.Add(customCard);

        taskbarCard = new FlatCard(L("card.taskbar"));
        var tf = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        tbAuto = new FlatToggle { RowWidth = 600, Text = L("chk.tbAuto") };
        tbLeft = new FlatNumber();
        tbLeft.Configure(0, 8000, 10);
        tbRight = new FlatNumber();
        tbRight.Configure(0, 8000, 10);
        tf.Controls.Add(tbAuto);
        tf.Controls.Add(Row(L("lbl.tbLeft"), tbLeft, 280));
        tf.Controls.Add(Row(L("lbl.tbRight"), tbRight, 280));
        tf.Controls.Add(Note(L("note.taskbar"), 560));
        taskbarCard.Controls.Add(tf);
        flow.Controls.Add(taskbarCard);

        position.SelectedIndexChanged += (_, _) =>
        {
            UpdatePositionGroups();
            SyncDefaultBar();
        };
        tbAuto.CheckedChanged += (_, _) => UpdatePositionGroups();
        monitor.SelectedIndexChanged += (_, _) => MoveToSelectedMonitor();
        moveWithMouse.Click += (_, _) => ToggleMouseMove();
        foreach (var n in new[] { posX, posY, posW }) n.ValueChanged += (_, _) => ApplyCustomBoxes();
        return page;
    }

    private static void OpenLog()
    {
        try
        {
            var path = Log.Path;
            if (File.Exists(path)) System.Diagnostics.Process.Start("notepad.exe", "\"" + path + "\"");
            else if (Directory.Exists(Path.GetDirectoryName(path))) System.Diagnostics.Process.Start("explorer.exe", "\"" + Path.GetDirectoryName(path) + "\"");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
        }
    }

    private void OnBarsEnabledChanged()
    {
        if (IsDisposed || barsEnabled is null) return;
        void Set()
        {
            var was = loading;
            loading = true;
            barsEnabled.Checked = host.Config.BarsEnabled;
            loading = was;
        }
        if (InvokeRequired) BeginInvoke(new Action(Set)); else Set();
    }

    // ---- weather ---------------------------------------------------------------------------------

    private static string CityName(GreekCity c) => Loc.IsGreek ? c.NameEl : c.NameEn;

    private void LoadMiniBounds()
    {
        if (miniW is null) return;
        var b = host.GetMiniWeatherBounds();
        miniW.SetValueSilently(host.Config.MiniWeatherWidth);
        miniH.SetValueSilently(host.Config.MiniWeatherHeight);
        miniX.SetValueSilently(b.X);
        miniY.SetValueSilently(b.Y);
    }

    private void OnMiniBoundsChanged()
    {
        if (!IsHandleCreated || IsDisposed) return;
        BeginInvoke(new Action(LoadMiniBounds));
    }

    private Control BuildDesktopBar()
    {
        var page = NewPage(out var flow);
        var mini = new FlatCard(Loc.T("pk.desktop.bar"));
        var miniFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        miniWeather = new FlatToggle { RowWidth = 600, Text = Loc.T("pk.show.a.bar.on") };
        miniFlow.Controls.Add(miniWeather);
        miniPicker = new SlotPicker();
        miniFlow.Controls.Add(miniPicker.Build());
        miniW = new FlatNumber();
        miniW.Configure(200, 4000, 10);
        miniH = new FlatNumber();
        miniH.Configure(24, 200, 2);
        miniX = new FlatNumber();
        miniX.Configure(-20000, 20000, 10);
        miniY = new FlatNumber();
        miniY.Configure(-20000, 20000, 10);
        miniFlow.Controls.Add(Row(L("lbl.width"), miniW, 240));
        miniFlow.Controls.Add(Row(L("lbl.height"), miniH, 240));
        miniFlow.Controls.Add(Row(L("lbl.x"), miniX, 240));
        miniFlow.Controls.Add(Row(L("lbl.y"), miniY, 240));
        var miniButtons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        miniMove = new FlatButton { Text = L("btn.moveMouse") };
        var miniCenter = new FlatButton { Text = L("btn.centerDesktop") };
        miniButtons.Controls.Add(miniMove);
        miniButtons.Controls.Add(miniCenter);
        miniFlow.Controls.Add(miniButtons);
        miniMove.Click += (_, _) =>
        {
            var on = !host.IsMiniWeatherEditMode;
            host.SetMiniWeatherEditMode(on);
            miniMove.Text = on ? L("btn.moveMouseDone") : L("btn.moveMouse");
        };
        miniCenter.Click += (_, _) =>
        {
            host.CenterMiniWeather();
            LoadMiniBounds();
        };
        miniOnTop = new FlatToggle { RowWidth = 600, Text = L("chk.miniOnTop") };
        miniFlow.Controls.Add(miniOnTop);
        miniShape = new FlatComboBox { Width = 300 };
        miniShape.SetItems(new[] { L("shape.rect"), L("shape.rounded"), L("shape.pill") }, 0);
        miniFlow.Controls.Add(Row(L("lbl.miniShape"), miniShape, 200));
        miniWhenHidden = new FlatToggle { RowWidth = 600, Text = L("chk.miniWhenHidden") };
        miniFlow.Controls.Add(miniWhenHidden);
        miniFlow.Controls.Add(Note(Loc.T("pk.a.free.bar.on"), 600));
        mini.Controls.Add(miniFlow);
        flow.Controls.Add(mini);

        return page;
    }

    private void ShowUpdateState()
    {
        if (updateStatus is null || updateStatus.IsDisposed) return;
        var u = host.AvailableUpdate;
        installUpdate.Visible = u is not null;
        openUpdatePage.Visible = u is not null;
        if (u is not null) updateStatus.Text = Loc.T("msg.updateAvailable", u.Version, AboutTexts.Version);
    }

    private async Task CheckUpdatesNowAsync(FlatButton button)
    {
        button.Enabled = false;
        installUpdate.Visible = false;
        openUpdatePage.Visible = false;
        updateStatus.Text = L("msg.checking");
        try
        {
            var found = await host.CheckForUpdatesAsync();
            if (IsDisposed) return;
            if (found is null) updateStatus.Text = Loc.T("msg.upToDate", AboutTexts.Version);
            ShowUpdateState();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            if (!IsDisposed) updateStatus.Text = Loc.T("msg.updateFailed", ex.Message);
        }
        finally
        {
            if (!button.IsDisposed) button.Enabled = true;
        }
    }

    private bool installingUpdate;

    private async Task InstallUpdateAsync()
    {
        if (host.AvailableUpdate is not { } u || installingUpdate) return;
        // Nothing to verify the file against: nothing is run, the release page opens for a manual download.
        if (!u.CanInstallAutomatically)
        {
            updateStatus.Text = L("upd.noDigest");
            OpenInBrowser(u.PageUrl);
            return;
        }
        var hosted = host.HostedByOpsis;   // inside Opsis the plugin files are in use: the update waits until Opsis is closed
        var question = hosted ? L("msg.confirmUpdateOnExit") : Loc.T("upd.confirm", u.Version);
        if (MessageBox.Show(this, question, hosted ? L("app.name") : L("upd.confirmTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        installingUpdate = true;
        installUpdate.Enabled = false;
        updateProgress.Value = 0;
        updateProgress.Visible = true;
        try
        {
            var progress = new Progress<double>(p =>
            {
                if (IsDisposed) return;
                updateProgress.Value = p;
                updateStatus.Text = Loc.T("msg.downloading", (int)Math.Round(p * 100));
            });
            var (path, sha256) = await UpdateInstaller.DownloadAsync(u, progress);
            if (IsDisposed) return;
            updateStatus.Text = L(hosted ? "msg.updateScheduled" : "msg.updateStarted");
            UpdateInstaller.LaunchAfterExit(path, sha256, hosted);
            if (!hosted)
            {
                host.RequestExit();
                // The helper waits for this process: if the normal exit is blocked (a dialog, a stuck window) the process ends anyway after a few seconds.
                _ = Task.Delay(8000).ContinueWith(_ => Environment.Exit(0), TaskScheduler.Default);
            }
        }
        catch (InvalidDataException)
        {
            if (!IsDisposed) updateStatus.Text = L("msg.verifyFailed");
        }
        catch (InvalidOperationException)
        {
            if (!IsDisposed) { updateStatus.Text = L("upd.noDigest"); OpenInBrowser(u.PageUrl); }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            if (!IsDisposed) updateStatus.Text = Loc.T("msg.updateFailed", ex.Message);
        }
        finally
        {
            installingUpdate = false;
            if (!IsDisposed)
            {
                updateProgress.Visible = false;
                installUpdate.Enabled = true;
            }
        }
    }

    // ---- read later ------------------------------------------------------------------------

    private static void OpenInBrowser(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
        }
    }

    // ---- the shares of a Euronext market ----------------------------------------------------

    /// <summary>The market's catalog (downloaded earlier or built in) plus whatever is already chosen from it.</summary>
    private IReadOnlyList<QuoteInstrument> CatalogFor(string mic)
    {
        var map = new Dictionary<string, QuoteInstrument>(StringComparer.Ordinal);
        if (mic == "XATH")
            foreach (var d in QuoteInstrument.Defaults().Where(d => d.Source == QuoteSource.Athens)) { d.Market = "XATH"; d.Enabled = false; map[d.Code] = d; }
        foreach (var c in catalogs.TryGetValue(mic, out var loaded) ? loaded : EuronextCatalog.LoadCached(mic)) map[c.Code] = c;
        foreach (var w in workWatch.Where(w => w.Market == mic && w.Source is QuoteSource.Athens or QuoteSource.Euronext)) map[w.Code] = w;
        return map.Values.OrderBy(i => i.DisplayName(Loc.IsGreek), StringComparer.OrdinalIgnoreCase).ToList();
    }

    private async void LoadCatalog(bool force)
    {
        var mic = SelectedMic;
        var cached = EuronextCatalog.LoadCached(mic);
        if (!force && (cached.Count > 0 || mic == "XATH" && catalogs.ContainsKey(mic)))
        {
            catalogs[mic] = cached;
            ShowStocks();
            return;
        }
        // Athens has a built-in list of the main shares; the rest is downloaded (names via OpenFIGI, about half a minute).
        ShowStocks();
        stockStatus.Text = L("msg.loadingList");
        refreshCatalog.Enabled = false;
        var progress = new Progress<string>(p => { if (!IsDisposed && SelectedMic == mic) stockStatus.Text = L("msg.loadingList") + "  " + p; });
        IReadOnlyList<QuoteInstrument> list;
        try
        {
            list = await EuronextCatalog.FetchAsync(mic, progress);
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
        {
            return;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or System.Text.Json.JsonException or FormatException or InvalidOperationException)
        {
            list = Array.Empty<QuoteInstrument>(); // shown as "could not be downloaded"
        }
        if (IsDisposed) return;
        refreshCatalog.Enabled = true;
        catalogs[mic] = list;
        if (SelectedMic == mic)
        {
            ShowStocks();
            if (list.Count == 0) stockStatus.Text = L("msg.listFailed");
        }
    }

    private void ShowStocks()
    {
        if (stockList is null) return;
        var mic = SelectedMic;
        var term = stockSearch.Value.Trim();
        var all = CatalogFor(mic);
        visibleStocks = all.Where(i => term.Length == 0 ||
            i.Symbol.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            i.NameEn.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            i.NameEl.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            i.Code.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        stockList.ClearRows();
        foreach (var i in visibleStocks)
            stockList.AddRow($"{i.Symbol}  —  {i.DisplayName(Loc.IsGreek)}   ({i.Unit})", IsChosen(i));
        UpdateStockStatus(all.Count);
    }

    private bool IsChosen(QuoteInstrument i) => workWatch.Any(w => w.Code == i.Code && w.Market == i.Market && w.Enabled);

    private void UpdateStockStatus(int total)
    {
        var chosen = workWatch.Count(w => w.Market == SelectedMic && w.Enabled);
        var stamp = EuronextCatalog.CacheTime(SelectedMic);
        stockStatus.Text = Loc.T("fmt.sharesCount", chosen, total) + (stamp is { } t ? "  ·  " + Loc.T("fmt.listDate", t.ToString("dd/MM/yyyy")) : "");
    }

    /// <summary>Ticks or unticks every share that is listed right now (the search filter applies).</summary>
    private void SetAllVisibleStocks(bool isChecked)
    {
        for (var i = 0; i < visibleStocks.Count; i++) ToggleStock(i, isChecked);
        ShowStocks();
    }

    private void ToggleStock(int index, bool isChecked)
    {
        if (index < 0 || index >= visibleStocks.Count) return;
        var inst = visibleStocks[index];
        var existing = workWatch.FirstOrDefault(w => w.Code == inst.Code && w.Market == inst.Market);
        var isDefault = QuoteInstrument.Defaults().Any(d => d.Code == inst.Code);
        if (isChecked)
        {
            if (existing is not null) existing.Enabled = true;
            else workWatch.Add(new QuoteInstrument(inst.Source, inst.Code, inst.Symbol, inst.NameEl, inst.NameEn, inst.Unit) { Market = inst.Market });
        }
        else if (existing is not null)
        {
            if (isDefault) existing.Enabled = false;
            else workWatch.Remove(existing); // the watch list only keeps what is shown
        }
        UpdateStockStatus(CatalogFor(SelectedMic).Count);
    }

    private static Control BuildInfoPage(Func<string> text, TickerHost? host = null)
    {
        var page = new Panel { Padding = new Padding(8, 6, 8, 6) };
        var box = new TextBox
        {
            Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 10f), TabStop = false, Tag = "surface",
            Text = text().Replace("\r\n", "\n").Replace("\n", "\r\n")
        };
        var frame = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 12, 8, 12), Tag = "surface" };
        frame.Controls.Add(box);
        page.Controls.Add(frame);
        if (host is not null)
        {
            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Padding = new Padding(16, 4, 8, 4) };
            var welcome = new FlatButton { Text = Loc.T("tray.welcome") };
            var family = new FlatButton { Text = Loc.T("tray.suite") };
            welcome.Click += (_, _) => host.ShowWelcome();
            family.Click += (_, _) => host.ShowSuite();
            bar.Controls.Add(welcome);
            bar.Controls.Add(family);
            page.Controls.Add(bar);   // Top, added after the Fill frame so it docks above it
        }
        return page;
    }

    // ---- loading / saving ----------------------------------------------------------------

    /// <summary>Number of pages that can be shown (every sub-page of every tab counts once; used by the snapshot test).</summary>
    public int TabCount => tabs.Count;

    /// <summary>Screen rectangle of the animated icon in the header (where a closing splash shrinks to); empty when it is not visible.</summary>
    public Rectangle BadgeScreenRect() => tabs.Badge is { IsHandleCreated: true, Visible: true } b ? b.RectangleToScreen(b.ClientRectangle) : Rectangle.Empty;

    /// <summary>Shows the page with the given index (0 = Settings / General).</summary>
    public void SelectTab(int index)
    {
        if (index < 0 || index >= tabs.Count) return;
        tabs.SelectedIndex = index;
    }

    public void LoadFromConfig()
    {
        var was = loading;
        loading = true;
        try
        {
            var c = host.Config;
            workTopics = (c.TopicWatches ?? new()).Select(t=>new TopicWatch {
                Name=t.Name, Terms=t.Terms.ToList(), Region=t.Region, Enabled=t.Enabled }).ToList();
            RebuildTopics();
            LoadWidgetStudio();
            var langs = Loc.Available();
            language.SelectSilently(Math.Max(0, langs.ToList().FindIndex(l => l.Code == Loc.Language)));
            perfMode.SelectSilently((int)PerformanceClassifier.ParseMode(c.PerformanceMode));
            networkCost.SelectSilently(c.NetworkCostMode == "metered" ? 1 : 0);
            UpdatePerformanceInfo();
            refresh.SetValueSilently(c.RefreshIntervalSeconds);
            speed.SetValueSilently(c.ScrollSpeed);
            speedMainB.SetValueSilently(c.MainSpeedB); speedSecond.SetValueSilently(c.SecondSpeed); speedSecondB.SetValueSilently(c.SecondSpeedB);
            speedMini.SetValueSilently(c.MiniSpeed); speedMiniB.SetValueSilently(c.MiniSpeedB); speedMonitor.SetValueSilently(c.MonitorSpeed); speedMonitorB.SetValueSilently(c.MonitorSpeedB);
            hoverPause.Checked = c.PauseOnHover;
            icons.Checked = c.ShowIcons;
            preview.Checked = c.ShowPreview;
            pauseOther.Checked = c.PauseOnOtherWindow;
            hideRead.Checked = c.HideRead;
            barsEnabled.Checked = c.BarsEnabled;
            startWithWindows.Checked = StartupRegistration.IsEnabled();
            toggleHotkey.Checked = c.ToggleHotkey;
            autoUpdate.Checked = c.CheckForUpdates;
            yahooHistory.Checked = c.HistoryFromYahoo;
            UpdateYahooUi();
            hazardRegion.SelectSilently(c.HazardRegion switch { "europe" => 1, "world" => 2, _ => 0 });
            hazardMinMagnitude.SelectSilently(Math.Max(0, Array.FindIndex(MagnitudeChoices, m => Math.Abs(m - c.HazardMinMagnitude) < 0.05)));
            hazardMaxAge.SetValueSilently(c.HazardMaxAgeHours);
            hazardRefresh.SetValueSilently(c.HazardRefreshMinutes);
            foreach (var (kind, check) in hazardKindChecks) check.Checked = c.HazardKinds.Count == 0 || c.HazardKinds.Contains(kind.ToString());
            foreach (var (source, check) in hazardSourceChecks) check.Checked = (c.HazardSources & (int)source) != 0;
            hazardInIcon.Checked = c.HazardInIcon;
            weatherInIcon.Checked = c.WeatherInIcon;
            workCustom = c.CustomPlaces.ToList();
            GreekCities.SetCustom(workCustom);   // what is saved, not what an earlier unsaved edit added
            FillAlertCities(string.IsNullOrEmpty(c.AlertCityId) ? null : c.AlertCityId);
            workCities = c.WeatherCities.ToList();
            ShowCities();
            miniWeather.Checked = c.MiniWeatherEnabled;
            LoadMiniBounds();
            miniOnTop.Checked = c.MiniWeatherOnTop;
            miniWhenHidden.Checked = c.MiniWhenTaskbarHidden;
            barShape.SelectSilently(c.BarShape switch { "rounded" => 1, "pill" => 2, _ => 0 });
            miniShape.SelectSilently(c.DesktopBarShape switch { "rounded" => 1, "pill" => 2, _ => 0 });

            mainPicker.Set(c.MainSlots());
            miniPicker.Set(c.MiniSlots());
            foreach (var (id, check) in weatherFieldChecks) check.Checked = c.WeatherFields.Contains(id);
            secondEnabled.Checked = c.SecondBarEnabled;
            secondPicker.Set(c.SecondSlots());
            foreach (var (screen, check, picker) in monitorRows)
            {
                var saved = c.MonitorBars.FirstOrDefault(m => string.Equals(m.Device, screen.DeviceName, StringComparison.OrdinalIgnoreCase));
                check.Checked = saved?.Enabled ?? false;
                picker.Set(saved?.Slots() ?? new BarSlots(new BarSlot(SlotKind.News), null));
            }

            position.SelectSilently(Math.Clamp((int)c.Position, 0, 4));
            followTaskbar.Checked = c.FollowTaskbar;
            reserve.Checked = host.EffectiveReserveSpace;
            var custom = host.GetCustomBounds();
            posX.SetValueSilently(custom.X);
            posY.SetValueSilently(custom.Y);
            posW.SetValueSilently(custom.Width);
            tbAuto.Checked = c.TaskbarAutoDetect;
            tbLeft.SetValueSilently(c.TaskbarLeftOffset);
            tbRight.SetValueSilently(c.TaskbarRightOffset);

            themeMode.SelectSilently(ThemeIndex(c.ThemeMode) is var ti && ti < themeMode.Count ? ti : 0); themeAccent.Checked = c.ThemeFollowAccent;
            opacity.Value = Math.Clamp(c.OpacityPercent, 30, 100);
            opacityLabel.Text = $"{opacity.Value} %";
            edgeFade.Checked = c.EdgeFade;
            windowOpacity.Value = Math.Clamp(c.SettingsOpacityPercent, 60, 100);
            windowOpacityLabel.Text = $"{windowOpacity.Value} %";
            // Inside Opsis the page is part of Opsis' own window, whose opacity is Opsis' setting.
            windowOpacityRow.Visible = !HostThemeLocked;
            barColor.SelectSilently(c.BarColorMode switch { "lightblue" => 1, "accent" => 2, "custom" => 3, _ => 0 });
            barSwatch.Rgb = c.BarCustomColor;
            barSwatch.Enabled = barColor.SelectedIndex == 3;
            taskbarColor.SelectSilently(c.TaskbarBarColorMode switch { "lightblue" => 1, "accent" => 2, "custom" => 3, _ => 0 });
            taskbarSwatch.Rgb = c.TaskbarBarCustomColor;
            taskbarSwatch.Enabled = taskbarColor.SelectedIndex == 3;
            taskbarBackdrop.Enabled = taskbarColor.SelectedIndex != 0;   // completely clear = no material at all
            barBackdrop.SelectSilently(c.BarBackdrop switch { "blur" => 1, "gradient" => 2, _ => 0 });
            taskbarBackdrop.SelectSilently(c.TaskbarBarBackdrop switch { "acrylic" => 0, "gradient" => 2, _ => 1 });
            breakingEnabled.Checked = c.BreakingEnabled;
            breakingSound.Checked = c.BreakingSound;
            weatherMapOnClick.Checked = c.WeatherMapOnClick;
            weatherAlerts.Checked = c.WeatherAlertsEnabled;
            alertCity.SelectSilently(string.IsNullOrEmpty(c.AlertCityId) ? 0 : Math.Max(0, GreekCities.All.ToList().FindIndex(x => x.Id == c.AlertCityId) + 1));
            keywords.Value = string.Join("; ", c.BreakingKeywords);
            textColorMode.SelectSilently(string.Equals(c.TextColorMode, "fixed", StringComparison.OrdinalIgnoreCase) ? 1 : 0);
            textShadow.Checked = c.TextShadow;
            mainFont.SelectSilently(FontIndex(c.MainFontFamily)); mainFontPct.SetValueSilently(c.MainFontPercent);
            secondFont.SelectSilently(FontIndex(c.SecondFontFamily)); secondFontPct.SetValueSilently(c.SecondFontPercent);
            miniFont.SelectSilently(FontIndex(c.MiniFontFamily)); miniFontPct.SetValueSilently(c.MiniFontPercent);
            monitorFont.SelectSilently(FontIndex(c.MonitorFontFamily)); monitorFontPct.SetValueSilently(c.MonitorFontPercent);
            mainHeightAuto.Checked = c.MainBarHeight == 0; mainHeight.SetValueSilently(c.MainBarHeight == 0 ? 42 : c.MainBarHeight); mainHeight.Enabled = c.MainBarHeight != 0;
            monitorHeightAuto.Checked = c.MonitorBarHeight == 0; monitorHeight.SetValueSilently(c.MonitorBarHeight == 0 ? 42 : c.MonitorBarHeight); monitorHeight.Enabled = c.MonitorBarHeight != 0;
            workDisabledFeeds = c.DisabledPluginFeeds.ToList();
            feedMaxLines.SetValueSilently(c.PluginFeedMaxLines);
            feedOrder.SelectSilently(c.PluginFeedOrder switch { "yearAsc" => 1, "yearDesc" => 2, _ => 0 });
            feedCountry.SelectSilently(Math.Max(0, feedCountryCodes.IndexOf(c.PluginFeedCountry)));
            RefreshHistoryFeeds();
            breakingSensitivity.SelectSilently((int)BreakingEngine.ParseSensitivity(c.BreakingSensitivity));
            forgetBreaking.Text = L("breaking.forget");
            maxAge.SetValueSilently(c.BreakingMaxAgeMinutes);
            duration.SetValueSilently(c.BreakingDurationMinutes);
            workLive = c.LiveFeeds.Select(LiveVideoStudio.Clean).ToList();
            RebuildLiveList();
            // The crawler's themes are its own, also inside Opsis; "Follow Opsis" is just one more choice there.
            themeRow.Visible = true; themeAccent.Visible = true;
            themeNote.Text = host.HostedByOpsis ? L("note.hostTheme") : "";

            newsMaxAge.SetValueSilently(c.NewsMaxAgeHours);
            newsNewestFirst.Checked = c.NewsSortNewestFirst;
            stockNewsOnly.Checked = c.StockNewsOnly;
            mutedNewsKeywords.Value = string.Join("; ", c.MutedNewsKeywords);
            workCategories = c.Categories.Select(Clone).ToList();
            RebuildCategoryLists(c.ActiveCategory);
            workWatch = c.Watchlist.Select(w => w.Clone()).ToList();
            workAlerts = c.PriceAlerts.Select(a => a.Clone()).ToList();
            alertChoicesSignature = "";
            RefreshAlertChoices();
            ShowAlerts();
            RebuildWatchLists();
            UpdatePositionGroups();
            UpdateBarsUi();
            SyncDefaultBar();
        }
        finally
        {
            loading = was;
        }
        moveWithMouse.Text = host.IsPositionEditMode ? L("btn.moveMouseDone") : L("btn.moveMouse");
        hasUnsavedChanges = false;
        if (status is not null && !status.IsDisposed) status.Text = L("settings.legend");
    }

    private void AttachDirtyTracking()
    {
        if (dirtyTrackingAttached || tabs is null) return;
        dirtyTrackingAttached = true;
        foreach (var control in Descendants(tabs))
        {
            switch (control)
            {
                case FlatCheckBox check:
                    check.CheckedChanged += (_, _) => MarkDirty();
                    break;
                case FlatComboBox combo:
                    combo.SelectedIndexChanged += (_, _) => MarkDirty();
                    break;
                case FlatNumber number:
                    number.ValueChanged += (_, _) => MarkDirty();
                    break;
                case FlatText text:
                    text.TextValueChanged += (_, _) => MarkDirty();
                    break;
                case FlatSlider slider:
                    slider.ValueChanged += (_, _) => MarkDirty();
                    break;
                case FlatList list:
                    list.ItemChecked += (_, _) => MarkDirty();
                    break;
                case FlatButton button when !button.IsNavigation:
                    button.Click += (_, _) =>
                    {
                        // Actions that only open dialogs, refresh data, or perform maintenance do not
                        // make the editable settings dirty by themselves.
                        var nonEditingActions = new[]
                        {
                            L("btn.resetRead"), L("btn.openLog"), L("btn.checkNow"),
                            L("btn.installUpdate"), L("btn.openPage"), L("btn.refreshList"),
                            L("btn.testBreaking"), L("btn.open"), L("settings.exportOpml"), L("settings.export"), L("settings.import")
                        };
                        if (!nonEditingActions.Contains(button.Text, StringComparer.CurrentCultureIgnoreCase)
                            && !button.Text.Contains("Export OPML", StringComparison.OrdinalIgnoreCase)
                            && !button.Text.Contains("Εξαγωγή OPML", StringComparison.OrdinalIgnoreCase))
                            MarkDirty();
                    };
                    break;
            }
        }
    }

    // ---- Windows 11 style rows: a sentence under the label of the main switches, and "Reset" on every card ----------------------

    /// <summary>"Detected: 2 logical processors, 7 GB of memory, no real graphics adapter. In use: Low — the number of processors."</summary>
    private void UpdatePerformanceInfo()
    {
        if (perfInfo is null || perfInfo.IsDisposed) return;
        if (!host.Performance.HasEvaluated) host.EvaluatePerformance();
        var p = host.Performance;
        var parts = new List<string>();
        if (p.Specs is { } s) parts.Add(Loc.T("perf.detected", s.LogicalCores, s.RamGb.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture), L(s.HasGpu ? "perf.gpuYes" : "perf.gpuNo")));
        var tierName = L("perf." + p.Tier.ToString().ToLowerInvariant());
        parts.Add(Loc.T("perf.now", tierName, L("perf.r." + p.Reason)));
        perfInfo.Text = string.Join(" ", parts);
    }

    private void DescribeToggles()
    {
        foreach (var (toggle, key) in new (FlatToggle?, string)[]
                 {
                     (barsEnabled as FlatToggle, "desc.barsEnabled"), (startWithWindows as FlatToggle, "desc.startWithWindows"),
                     (hoverPause as FlatToggle, "desc.hoverPause"), (hideRead as FlatToggle, "desc.hideRead"), (tbAuto as FlatToggle, "desc.tbAuto"),
                     (themeAccent as FlatToggle, "desc.themeAccent"), (edgeFade as FlatToggle, "desc.edgeFade"), (breakingSound as FlatToggle, "desc.breakingSound")
                 })
            if (toggle is not null) toggle.Description = L(key);
        if (status is not null) status.Tag = "muted";
    }

    private static bool IsSettingControl(Control c) => c is FlatCheckBox or FlatComboBox or FlatNumber or FlatSlider or FlatText;

    private void EnableCardResets()
    {
        foreach (var card in Descendants(tabs).OfType<FlatCard>())
        {
            if (!Descendants(card).Any(IsSettingControl)) continue;
            if (card.Tag is string tag && tag.StartsWith("p1-widget-", StringComparison.Ordinal))
            {
                var kind = tag["p1-widget-".Length..];
                card.ShowReset = true;
                card.ResetRequested += (_, _) =>
                {
                    if (!widgetEditors.TryGetValue(kind, out var controls)) return;
                    loading = true;
                    try
                    {
                        controls.Layout.SelectSilently(0);
                        controls.Count.SetValueSilently(12);
                        controls.Source.Checked = false;
                        controls.Time.Checked = false;
                        if (widgetAccentOptions.TryGetValue(kind, out var accent))
                            accent.Checked = false;
                    }
                    finally { loading = false; }
                    MarkDirty();
                };
                continue;
            }
            card.ShowReset = true;
            card.ResetRequested += (_, _) => ResetCard(card);
        }
    }

    private static object? CaptureValue(Control c) => c switch
    {
        FlatCheckBox check => check.Checked,
        FlatComboBox combo => combo.SelectedIndex,
        FlatNumber number => number.Value,
        FlatSlider slider => slider.Value,
        FlatText text => text.Value,
        _ => null
    };

    private static void RestoreValue(Control c, object? value)
    {
        switch (c)
        {
            case FlatCheckBox check when value is bool b: check.Checked = b; break;
            case FlatComboBox combo when value is int i: combo.SelectedIndex = i; break;
            case FlatNumber number when value is int n: number.Value = n; break;
            case FlatSlider slider when value is int s: slider.Value = s; break;
            case FlatText text when value is string v: text.Value = v; break;
        }
    }

    /// <summary>
    /// "Reset" of one card: its controls get the default values, every other control keeps what it shows now. Nothing is saved until the user presses Apply.
    /// The defaults are found by loading a default configuration into the panel for a moment (the plain settings of the real configuration are put back
    /// right after), so the card, its labels and the other pages stay consistent with the settings code.
    /// </summary>
    private void ResetCard(FlatCard card)
    {
        var controls = Descendants(tabs).Where(IsSettingControl).ToList();
        var inCard = Descendants(card).Where(IsSettingControl).ToHashSet();
        var before = controls.ToDictionary(c => c, CaptureValue);
        var listsCategories = workCategories; var listsWatch = workWatch; var listsAlerts = workAlerts; var listsLive = workLive;
        var saved = new AppConfig();
        AppConfig.CopyScalars(host.Config, saved);
        try
        {
            AppConfig.CopyScalars(new AppConfig(), host.Config);
            LoadFromConfig();
            var defaults = controls.Where(inCard.Contains).ToDictionary(c => c, CaptureValue);
            loading = true;
            try
            {
                foreach (var c in controls) RestoreValue(c, inCard.Contains(c) ? defaults[c] : before[c]);
                workCategories = listsCategories; workWatch = listsWatch; workAlerts = listsAlerts; workLive = listsLive;
                RebuildCategoryLists(host.Config.ActiveCategory);
                RebuildWatchLists();
                RefreshAlertChoices();
                ShowAlerts();
                RebuildLiveList();
            }
            finally { loading = false; }
        }
        finally
        {
            AppConfig.CopyScalars(saved, host.Config);
        }
        MarkDirty();
        if (status is not null && !status.IsDisposed) status.Text = L("card.resetDone");
        Theme.Apply(this);
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    private readonly System.Windows.Forms.Timer autoApplyTimer = new() { Interval = 600 };

    /// <summary>A change was made: it is saved and applied about 0.6 s later (typing and dragging a slider make one save, not one per key).</summary>
    private void MarkDirty()
    {
        if (loading || !built) return;
        hasUnsavedChanges = true;
        autoApplyTimer.Stop();
        autoApplyTimer.Start();
    }

    /// <summary>Saves a pending change at once (closing, hiding, leaving the page).</summary>
    public void FlushPendingChanges()
    {
        autoApplyTimer.Stop();
        if (hasUnsavedChanges && built && !loading && !IsDisposed) ApplySettings();
    }

    /// <summary>Called before the window is hidden: a pending change is saved (there is no "unsaved changes" question any more); always true.</summary>
    public bool ConfirmCloseIfNeeded()
    {
        FlushPendingChanges();
        return true;
    }

    private static FeedCategory Clone(FeedCategory c) => new()
    {
        Id = c.Id, NameEl = c.NameEl, NameEn = c.NameEn, BuiltIn = c.BuiltIn,
        Feeds = c.Feeds.Select(f => new FeedSource(f.Name, f.Url) { Enabled = f.Enabled }).ToList()
    };

    private IEnumerable<SlotPicker> AllPickers()
    {
        if (mainPicker is not null) yield return mainPicker;
        if (secondPicker is not null) yield return secondPicker;
        if (miniPicker is not null) yield return miniPicker;
        foreach (var row in monitorRows) yield return row.Picker;
    }

    private void RebuildCategoryLists(string? activeId)
    {
        category.SetItems(workCategories.Select(c => c.DisplayName), 0);
        categoryList.ClearRows();
        foreach (var cat in workCategories) categoryList.AddRow(cat.DisplayName);
        var index = Math.Max(0, workCategories.FindIndex(c => string.Equals(c.Id, activeId, StringComparison.OrdinalIgnoreCase)));
        category.SelectSilently(index);
        if (categoryList.Items.Count > 0) categoryList.SelectedIndex = index;
        foreach (var picker in AllPickers()) picker.SetChoices(workCategories);
    }

    private void UpdateYahooUi()
    {
        var on = yahooHistory.Checked;
        foreach (var c in new Control[] { yahooSearch, yahooSearchButton, yahooAddButton, yahooRemoveButton, yahooHitList, yahooGroup }.Concat(yahooPresetButtons)) c.Enabled = on;
        if (!on) yahooStatus.Text = L("yahoo.off");
        else if (yahooStatus.Text.Length == 0 || yahooStatus.Text == L("yahoo.off")) yahooStatus.Text = L("yahoo.localHint");
    }

    /// <summary>Lists the built-in catalogue (the chosen group, filtered by the text typed); the ticked rows are added with the button.</summary>
    private void ShowYahooCatalogue()
    {
        if (yahooHitList is null || yahooHitList.IsDisposed) return;
        var groupKey = yahooGroup.SelectedIndex > 0 && yahooGroup.SelectedIndex <= YahooPresets.All.Count ? YahooPresets.All[yahooGroup.SelectedIndex - 1].Key : null;
        var items = YahooPresets.Filter(groupKey, yahooSearch.Value);
        yahooHits = items.Select(x => new YahooHit(x.Item.Symbol, Loc.IsGreek ? x.Item.NameEl : x.Item.NameEn, L("yahoo.group." + x.GroupKey), "")).ToList();
        yahooHitList.ClearRows();
        foreach (var h in yahooHits) yahooHitList.AddRow($"{h.Symbol}  —  {h.Name}   ({h.Type})", false);
        yahooStatus.Text = yahooHits.Count == 0 ? L("yahoo.none") : L("yahoo.localHint");
    }

    private async Task RunYahooSearchAsync()
    {
        var query = yahooSearch.Value.Trim();
        if (query.Length < 2 || !yahooHistory.Checked) return;
        // the switch is applied with the other settings; a search needs it now
        YahooAccess.Enabled = true;
        yahooSearchButton.Enabled = false;
        yahooStatus.Text = Loc.T("pk.testing");
        try
        {
            yahooHits = (await YahooSearch.SearchAsync(query, CancellationToken.None)).ToList();
            yahooHitList.ClearRows();
            foreach (var h in yahooHits) yahooHitList.AddRow($"{h.Symbol}  —  {h.Name}   ({h.Type}, {h.Exchange})", false);
            yahooStatus.Text = yahooHits.Count == 0 ? L("yahoo.none") : Loc.T("yahoo.found", yahooHits.Count);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            yahooStatus.Text = Loc.T("yahoo.error", ex.Message);
        }
        finally
        {
            if (!IsDisposed) { yahooSearchButton.Enabled = yahooHistory.Checked; YahooAccess.Enabled = host.Config.HistoryFromYahoo; }
        }
    }

    /// <summary>Adds Yahoo instruments to the watch list (shown on the bars); at most <see cref="YahooAccess.MaxInstruments"/> of them.</summary>
    private void AddYahoo(IEnumerable<(string Symbol, string NameEl, string NameEn)> items)
    {
        var added = 0;
        foreach (var (symbol, el, en) in items)
        {
            if (workWatch.Any(w => w.Source == QuoteSource.Yahoo && string.Equals(w.Code, symbol, StringComparison.OrdinalIgnoreCase))) continue;
            if (workWatch.Count(w => w.Source == QuoteSource.Yahoo) >= YahooAccess.MaxInstruments)
            {
                yahooStatus.Text = Loc.T("yahoo.limit", YahooAccess.MaxInstruments);
                break;
            }
            workWatch.Add(new QuoteInstrument(QuoteSource.Yahoo, symbol, symbol, el, en, ""));
            added++;
        }
        if (added > 0)
        {
            RebuildWatchLists();
            MarkDirty();
            yahooStatus.Text = Loc.T("yahoo.added", added);
        }
    }

    private void RemoveSelectedYahoo()
    {
        if (!watchLists.TryGetValue(QuoteSource.Yahoo, out var list) || list.SelectedIndex < 0) return;
        var items = workWatch.Where(i => i.Source == QuoteSource.Yahoo).ToList();
        if (list.SelectedIndex >= items.Count) return;
        workWatch.Remove(items[list.SelectedIndex]);
        RebuildWatchLists();
        MarkDirty();
    }

    private void RebuildWatchLists()
    {
        foreach (var (source, list) in watchLists)
        {
            list.ClearRows();
            foreach (var w in workWatch.Where(i => i.Source == source))
                list.AddRow($"{w.Symbol}  —  {w.DisplayName(Loc.IsGreek)}   ({w.Unit})", w.Enabled);
        }
        ShowStocks();
    }

    private void ApplySettings()
    {
        var c = host.Config;
        c.WidgetProfiles = ReadWidgetStudio();
        c.TopicWatches = workTopics.Select(t => new TopicWatch { Name=t.Name, Terms=t.Terms.ToList(),
            Region=t.Region, Enabled=t.Enabled }).ToList();
        var mutedKeywords = mutedNewsKeywords.Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).Take(100).ToList();
        var newsSettingsChanged = c.NewsMaxAgeHours != newsMaxAge.Value || c.NewsSortNewestFirst != newsNewestFirst.Checked || c.StockNewsOnly != stockNewsOnly.Checked || !c.MutedNewsKeywords.SequenceEqual(mutedKeywords, StringComparer.OrdinalIgnoreCase);
        c.Language = Loc.Available().ElementAtOrDefault(Math.Max(0, language.SelectedIndex)).Code ?? c.Language;
        c.PerformanceMode = PerformanceClassifier.ModeText((PerformanceMode)Math.Clamp(perfMode.SelectedIndex, 0, 3));
        c.NetworkCostMode = networkCost.SelectedIndex == 1 ? "metered" : "normal";
        c.NewsRefreshSeconds = refresh.Value;
        c.ScrollSpeed = speed.Value;
        c.MainSpeedB = AppConfig.CleanSpeed(speedMainB.Value); c.SecondSpeed = AppConfig.CleanSpeed(speedSecond.Value); c.SecondSpeedB = AppConfig.CleanSpeed(speedSecondB.Value);
        c.MiniSpeed = AppConfig.CleanSpeed(speedMini.Value); c.MiniSpeedB = AppConfig.CleanSpeed(speedMiniB.Value);
        c.MonitorSpeed = AppConfig.CleanSpeed(speedMonitor.Value); c.MonitorSpeedB = AppConfig.CleanSpeed(speedMonitorB.Value);
        c.PauseOnHover = hoverPause.Checked;
        c.ShowIcons = icons.Checked;
        c.ShowPreview = preview.Checked;
        c.PauseOnOtherWindow = pauseOther.Checked;
        c.HideRead = hideRead.Checked;
        c.NewsMaxAgeHours = newsMaxAge.Value;
        c.NewsSortNewestFirst = newsNewestFirst.Checked;
        c.StockNewsOnly = stockNewsOnly.Checked;
        c.MutedNewsKeywords = mutedKeywords;
        c.CheckForUpdates = autoUpdate.Checked;
        c.HistoryFromYahoo = yahooHistory.Checked;
        c.ToggleHotkey = toggleHotkey.Checked;
        c.HazardRegion = hazardRegion.SelectedIndex switch { 1 => "europe", 2 => "world", _ => "greece" };
        c.HazardMinMagnitude = MagnitudeChoices[Math.Clamp(hazardMinMagnitude.SelectedIndex, 0, MagnitudeChoices.Length - 1)];
        c.HazardMaxAgeHours = hazardMaxAge.Value;
        c.HazardRefreshMinutes = hazardRefresh.Value;
        var chosenKinds = hazardKindChecks.Where(kv => kv.Value.Checked).Select(kv => kv.Key).ToList();
        c.HazardKinds = chosenKinds.Count == hazardKindChecks.Count || chosenKinds.Count == 0 ? new List<string>() : chosenKinds.Select(k => k.ToString()).ToList();
        c.HazardSources = hazardSourceChecks.Where(kv => kv.Value.Checked).Sum(kv => (int)kv.Key);
        c.HazardInIcon = hazardInIcon.Checked;
        c.WeatherInIcon = weatherInIcon.Checked;
        c.CustomPlaces = workCustom.ToList();
        c.WeatherCities = workCities.ToList();
        c.MiniWeatherEnabled = miniWeather.Checked;
        c.MiniWeatherWidth = miniW.Value;
        c.MiniWeatherHeight = miniH.Value;
        // Still in the middle of the desktop? Then keep "centred" (it follows the screen) instead of storing numbers.
        var work = (Screen.PrimaryScreen ?? Screen.AllScreens[0]).WorkingArea;
        var cw = Math.Min(miniW.Value, work.Width);
        var centredX = work.Left + (work.Width - cw) / 2;
        var centredY = work.Top + (work.Height - miniH.Value) / 2;
        if (miniX.Value == centredX && miniY.Value == centredY) { c.MiniWeatherX = int.MinValue; c.MiniWeatherY = int.MinValue; }
        else { c.MiniWeatherX = miniX.Value; c.MiniWeatherY = miniY.Value; }
        c.MiniWeatherOnTop = miniOnTop.Checked;
        c.MiniWhenTaskbarHidden = miniWhenHidden.Checked;
        c.BarShape = barShape.SelectedIndex switch { 1 => "rounded", 2 => "pill", _ => "rect" };
        c.DesktopBarShape = miniShape.SelectedIndex switch { 1 => "rounded", 2 => "pill", _ => "rect" };

        var mainSlots = mainPicker.Get();
        c.MainSlotA = mainSlots.A.ToSpec();
        c.MainSlotB = mainSlots.B?.ToSpec() ?? "";
        c.MainContent = mainSlots.ToLegacy();
        var miniSlots = miniPicker.Get();
        c.MiniSlotA = miniSlots.A.ToSpec();
        c.MiniSlotB = miniSlots.B?.ToSpec() ?? "";
        c.WeatherFields = weatherFieldChecks.Where(kv => kv.Value.Checked).Select(kv => kv.Key).ToList();
        c.SecondBarEnabled = secondEnabled.Checked && position.SelectedIndex != (int)TickerPosition.TaskbarIntegrated;
        var secondSlots = secondPicker.Get();
        c.SecondSlotA = secondSlots.A.ToSpec();
        c.SecondSlotB = secondSlots.B?.ToSpec() ?? "";
        c.SecondContent = secondSlots.ToLegacy() == BarContent.Both ? BarContent.Stocks : secondSlots.ToLegacy();
        // Extra bars of the monitors shown here; entries of monitors that are not connected right now are kept.
        var shown = monitorRows.Select(r => r.Screen.DeviceName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        c.MonitorBars = c.MonitorBars.Where(m => !shown.Contains(m.Device)).Concat(monitorRows.Select(r =>
            new MonitorBar { Device = r.Screen.DeviceName, Enabled = r.Check.Checked, Content = r.Picker.Get().ToLegacy(), SlotA = r.Picker.Get().A.ToSpec(), SlotB = r.Picker.Get().B?.ToSpec() ?? "" })).ToList();

        c.Position = (TickerPosition)Math.Max(0, position.SelectedIndex);
        c.FollowTaskbar = followTaskbar.Checked;
        c.ReserveScreenSpace = reserve.Checked;
        c.CustomX = posX.Value;
        c.CustomY = posY.Value;
        c.CustomWidth = posW.Value;
        c.TaskbarAutoDetect = tbAuto.Checked;
        c.TaskbarLeftOffset = tbLeft.Value;
        c.TaskbarRightOffset = tbRight.Value;

        c.ThemeMode = ThemeNameAt(themeMode.SelectedIndex); c.ThemeFollowAccent = themeAccent.Checked;
        c.OpacityPercent = opacity.Value;
        c.EdgeFade = edgeFade.Checked;
        if (!HostThemeLocked) c.SettingsOpacityPercent = windowOpacity.Value;
        c.BarBackdrop = barBackdrop.SelectedIndex switch { 1 => "blur", 2 => "gradient", _ => "acrylic" };
        c.TaskbarBarBackdrop = taskbarBackdrop.SelectedIndex switch { 0 => "acrylic", 2 => "gradient", _ => "blur" };
        c.BarColorMode = barColor.SelectedIndex switch { 1 => "lightblue", 2 => "accent", 3 => "custom", _ => "theme" };
        c.BarCustomColor = barSwatch.Rgb;
        c.TaskbarBarColorMode = taskbarColor.SelectedIndex switch { 1 => "lightblue", 2 => "accent", 3 => "custom", _ => "taskbar" };
        c.TaskbarBarCustomColor = taskbarSwatch.Rgb;
        c.BreakingEnabled = breakingEnabled.Checked;
        c.BreakingSound = breakingSound.Checked;
        c.WeatherMapOnClick = weatherMapOnClick.Checked;
        c.WeatherAlertsEnabled = weatherAlerts.Checked;
        c.AlertCityId = alertCity.SelectedIndex > 0 && alertCity.SelectedIndex <= GreekCities.All.Count ? GreekCities.All[alertCity.SelectedIndex - 1].Id : "";
        c.PluginFeedMaxLines = feedMaxLines.Value;
        c.PluginFeedOrder = feedOrder.SelectedIndex switch { 1 => "yearAsc", 2 => "yearDesc", _ => "priority" };
        c.PluginFeedCountry = feedCountryCodes.ElementAtOrDefault(Math.Max(0, feedCountry.SelectedIndex)) ?? "";
        c.DisabledPluginFeeds = workDisabledFeeds.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        c.TextColorMode = textColorMode.SelectedIndex == 1 ? "fixed" : "auto";
        c.TextShadow = textShadow.Checked;
        c.MainFontFamily = FontAt(mainFont); c.MainFontPercent = mainFontPct.Value;
        c.SecondFontFamily = FontAt(secondFont); c.SecondFontPercent = secondFontPct.Value;
        c.MiniFontFamily = FontAt(miniFont); c.MiniFontPercent = miniFontPct.Value;
        c.MonitorFontFamily = FontAt(monitorFont); c.MonitorFontPercent = monitorFontPct.Value;
        c.MainBarHeight = mainHeightAuto.Checked ? 0 : mainHeight.Value;
        c.MonitorBarHeight = monitorHeightAuto.Checked ? 0 : monitorHeight.Value;
        c.BreakingKeywords = keywords.Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        c.BreakingSensitivity = BreakingEngine.SensitivityText((BreakingSensitivity)Math.Clamp(breakingSensitivity.SelectedIndex, 0, 2));
        c.BreakingMaxAgeMinutes = maxAge.Value;
        c.BreakingDurationMinutes = duration.Value;
        c.LiveFeeds = LiveVideoStudio.CleanAll(workLive);

        // Feeds: write the edited copy back and fetch again when the active category's sources changed.
        var previousActive = c.ActiveCategory;
        var before = FeedSignature(c.Categories);
        c.Categories = workCategories.Select(Clone).ToList();
        var chosen = category.SelectedIndex >= 0 && category.SelectedIndex < c.Categories.Count ? c.Categories[category.SelectedIndex].Id : previousActive;
        var feedsChanged = before != FeedSignature(c.Categories);
        c.Watchlist = workWatch.Select(w => w.Clone()).ToList();
        c.PriceAlerts = workAlerts.Select(a => a.Clone()).ToList();

        host.ApplyConfig();
        host.StartHistoryBackfill();
        host.SetBarsEnabled(barsEnabled.Checked);
        if (!host.HostedByOpsis) StartupRegistration.Set(startWithWindows.Checked);
        host.SetCategory(chosen);
        if ((feedsChanged || newsSettingsChanged) && string.Equals(chosen, previousActive, StringComparison.OrdinalIgnoreCase)) host.ReloadNews();
        host.SaveConfig();
        hasUnsavedChanges = false;

        SetStatus(L("msg.saved"));
        Applied?.Invoke();
    }

    private void ExportSettings()
    {
        using var dialog = new SaveFileDialog
        {
            Title = L("settings.exportTitle"),
            Filter = "JSON settings (*.json)|*.json|All files (*.*)|*.*",
            FileName = "opsis-newscrawler-settings.json",
            DefaultExt = "json",
            AddExtension = true,
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
        var tempDirectory = Path.Combine(Path.GetTempPath(), "OpsisNewsCrawlerExport", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDirectory);
            var tempFile = Path.Combine(tempDirectory, "settings.json");
            host.Config.Save(tempFile);
            if (!File.Exists(tempFile) || new FileInfo(tempFile).Length == 0)
                throw new IOException("The settings file could not be written.");
            File.Copy(tempFile, dialog.FileName, overwrite: true);
            SetStatus(L("settings.exported"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(FindForm(), ex.Message, L("settings.exportTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            try { Directory.Delete(tempDirectory, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private void ImportSettings()
    {
        using var dialog = new OpenFileDialog
        {
            Title = L("settings.importTitle"),
            Filter = "JSON settings (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
        if (MessageBox.Show(FindForm(), L("settings.importConfirm"), L("settings.importTitle"),
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;

        try
        {
            // Validate the JSON before AppConfig.Load applies defaults; malformed imports must never silently
            // replace the user's current configuration with a fresh default profile.
            using (var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(dialog.FileName)))
            {
                if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
                    throw new System.Text.Json.JsonException("The settings file must contain a JSON object.");
            }
            var tempDirectory = Path.Combine(Path.GetTempPath(), "OpsisNewsCrawlerImport", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            AppConfig imported;
            try
            {
                var tempConfig = Path.Combine(tempDirectory, "config.json");
                File.Copy(dialog.FileName, tempConfig, overwrite: true);
                imported = AppConfig.Load(tempConfig);
            }
            finally
            {
                try { Directory.Delete(tempDirectory, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
            foreach (var property in typeof(AppConfig).GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
            {
                if (!property.CanRead || property.SetMethod is null || !property.SetMethod.IsPublic || property.GetIndexParameters().Length != 0) continue;
                property.SetValue(host.Config, property.GetValue(imported));
            }
            host.ApplyConfig();
            host.SetBarsEnabled(host.Config.BarsEnabled);
            host.SetCategory(host.Config.ActiveCategory);
            host.ReloadNews();
            host.SaveConfig();
            LoadFromConfig();
            SetStatus(L("settings.imported"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or System.Reflection.TargetInvocationException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            MessageBox.Show(FindForm(), L("settings.importFailed") + Environment.NewLine + ex.Message,
                L("settings.importTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string FeedSignature(IEnumerable<FeedCategory> cats) =>
        string.Join("|", cats.Select(c => c.Id + ":" + string.Join(",", c.Feeds.Select(f => f.Url + (f.Enabled ? "+" : "-")))));

    private void SetStatus(string text)
    {
        status.Text = text;
        var timer = new System.Windows.Forms.Timer { Interval = 4000 };
        timer.Tick += (_, _) =>
        {
            if (!status.IsDisposed) status.Text = L("settings.legend");
            timer.Dispose();
        };
        timer.Start();
    }

    // ---- bars / position handlers ---------------------------------------------------------

    private static string SplitNote() => Loc.T("pk.split.in.the.middle");

    /// <summary>Two drop-downs that say what a bar shows: the first part (or the whole bar) and an optional second part (the bar is then split in the middle).</summary>
    private sealed class SlotPicker
    {
        private readonly FlatComboBox a = new() { Width = 380 };
        private readonly FlatComboBox b = new() { Width = 380 };
        private List<BarSlot> choices = new();
        private BarSlots current = new(new BarSlot(SlotKind.News), null);
        private bool suppress;

        public SlotPicker()
        {
            a.SelectedIndexChanged += (_, _) => Read();
            b.SelectedIndexChanged += (_, _) => Read();
        }

        public BarSlots Get() => current;

        public void Set(BarSlots value)
        {
            current = value;
            Apply();
        }

        public void SetEnabled(bool on) => a.Enabled = b.Enabled = on;

        public Control Build()
        {
            var box = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            box.Controls.Add(Row(Loc.T("pk.content.or.left.half"), a, 260));
            box.Controls.Add(Row(Loc.T("pk.right.half.split.in"), b, 260));
            return box;
        }

        public void SetChoices(IEnumerable<FeedCategory> categories)
        {
            choices = new List<BarSlot> { new(SlotKind.News) };
            var labels = new List<string> { Loc.T("pk.news.active.category") };
            foreach (var c in categories)
            {
                choices.Add(new BarSlot(SlotKind.News, c.Id));
                labels.Add(Loc.T("pk.news") + c.DisplayName);
            }
            choices.Add(new BarSlot(SlotKind.Stocks));
            labels.Add(Loc.T("content.stocks"));
            choices.Add(new BarSlot(SlotKind.Weather));
            labels.Add(Loc.T("content.weather"));
            choices.Add(new BarSlot(SlotKind.Hazards));
            labels.Add(Loc.T("hazard.slot"));
            foreach (var f in PluginFeedMapper.AllFeeds()) { choices.Add(new BarSlot(SlotKind.Feed, f.Key)); labels.Add(Loc.Pick(f.TitleEl, f.TitleEn)); }

            suppress = true;
            try
            {
                a.SetItems(labels, 0);
                b.SetItems(new[] { Loc.T("pk.none.the.whole.bar") }.Concat(labels), 0);
            }
            finally { suppress = false; }
            Apply();
        }

        private int IndexOf(BarSlot slot)
        {
            var i = choices.FindIndex(x => x.Kind == slot.Kind && (slot.Kind is not (SlotKind.News or SlotKind.Feed) || string.Equals(x.Category ?? "", slot.Category ?? "", StringComparison.OrdinalIgnoreCase)));
            return Math.Max(0, i);
        }

        private void Apply()
        {
            if (choices.Count == 0) return;
            suppress = true;
            try
            {
                a.SelectSilently(IndexOf(current.A));
                b.SelectSilently(current.B is { } second ? IndexOf(second) + 1 : 0);
            }
            finally { suppress = false; }
        }

        private void Read()
        {
            if (suppress || choices.Count == 0 || a.SelectedIndex < 0) return;
            var first = choices[Math.Clamp(a.SelectedIndex, 0, choices.Count - 1)];
            BarSlot? second = b.SelectedIndex > 0 ? choices[Math.Clamp(b.SelectedIndex - 1, 0, choices.Count - 1)] : null;
            if (second is { } s && s == first) second = null;
            current = new BarSlots(first, second);
        }
    }

    private void UpdateBarsUi()
    {
        var integratedMain = position.SelectedIndex == (int)TickerPosition.TaskbarIntegrated;
        secondEnabled.Enabled = !integratedMain;
        secondPicker.SetEnabled(secondEnabled.Checked && !integratedMain);
        secondNote.Text = integratedMain ? L("note.secondBarBlocked") : L("note.secondBar");
    }

    /// <summary>The "default bar" choice of the Bars tab mirrors the position: above the taskbar, inside it, or something else.</summary>
    private void SyncDefaultBar()
    {
        if (defaultBar is null || position is null) return;
        var index = position.SelectedIndex switch
        {
            (int)TickerPosition.BottomTaskbar => 0,
            (int)TickerPosition.TaskbarIntegrated => 1,
            _ => 2
        };
        defaultBar.SelectSilently(index);
    }

    private void UpdatePositionGroups()
    {
        var p = (TickerPosition)Math.Max(0, position.SelectedIndex);
        customCard.Visible = p == TickerPosition.Custom;
        taskbarCard.Visible = p == TickerPosition.TaskbarIntegrated;
        reserve.Enabled = p is TickerPosition.BottomTaskbar or TickerPosition.Top;
        followTaskbar.Enabled = p == TickerPosition.BottomTaskbar;
        tbLeft.Enabled = tbRight.Enabled = !tbAuto.Checked;
        if (built) UpdateBarsUi();
    }

    private void ApplyCustomBoxes()
    {
        if (loading) return;
        host.SetCustomBounds(posX.Value, posY.Value, posW.Value);
        if (host.Config.Position == TickerPosition.Custom) host.Window.Reposition();
    }

    private void MoveToSelectedMonitor()
    {
        if (loading || monitor.SelectedIndex < 0 || monitor.SelectedIndex >= Screen.AllScreens.Length) return;
        var wa = Screen.AllScreens[monitor.SelectedIndex].WorkingArea;
        loading = true;
        posX.SetValueSilently(wa.Left + 100);
        posY.SetValueSilently(wa.Bottom - 42 - 40);
        posW.SetValueSilently(Math.Min(900, wa.Width - 200));
        loading = false;
        ApplyCustomBoxes();
    }

    private void ToggleMouseMove()
    {
        var on = !host.IsPositionEditMode;
        if (on)
        {
            // Make sure the strip is in Custom position right now, with the boxes' values.
            position.SelectSilently((int)TickerPosition.Custom);
            UpdatePositionGroups();
            host.Config.Position = TickerPosition.Custom;
            host.SetCustomBounds(posX.Value, posY.Value, posW.Value);
            host.SetPosition(TickerPosition.Custom);
        }
        host.SetPositionEditMode(on);
        moveWithMouse.Text = on ? L("btn.moveMouseDone") : L("btn.moveMouse");
    }

    private void OnCustomBoundsChanged()
    {
        if (!IsHandleCreated || IsDisposed) return;
        BeginInvoke(new Action(() =>
        {
            posX.SetValueSilently(host.Config.CustomX);
            posY.SetValueSilently(host.Config.CustomY);
            posW.SetValueSilently(host.Config.CustomWidth);
        }));
    }

    // ---- source handlers -------------------------------------------------------------------

    private string WorldFeedContinentKey() => WorldPlaces.Continents.ElementAtOrDefault(Math.Max(0, worldFeedContinent.SelectedIndex))?.Key ?? "europe";

    private void FillWorldFeedCountries()
    {
        worldFeedCountry.SetItems(WorldFeeds.Countries(WorldFeedContinentKey()).Select(c => c.Name), 0);
        FillWorldFeeds();
    }

    private IReadOnlyList<WorldFeed> shownWorldFeeds = Array.Empty<WorldFeed>();

    private void FillWorldFeeds()
    {
        var countries = WorldFeeds.Countries(WorldFeedContinentKey());
        var country = countries.ElementAtOrDefault(Math.Max(0, worldFeedCountry.SelectedIndex));
        shownWorldFeeds = country.Code is null ? Array.Empty<WorldFeed>() : WorldFeeds.Of(WorldFeedContinentKey(), country.Code);
        worldFeedList.ClearRows();
        foreach (var f in shownWorldFeeds) worldFeedList.AddRow($"{f.Name}   -   {new Uri(f.Url).Host}");
        if (shownWorldFeeds.Count > 0) worldFeedList.SelectedIndex = 0;
    }

    /// <summary>Adds the chosen feed (or all feeds of the country) to the selected news category; a feed that is already there is skipped.</summary>
    private void AddWorldFeeds(bool onlySelected)
    {
        if (SelectedCategory() is not { } cat) return;
        var chosen = onlySelected
            ? shownWorldFeeds.Where((_, i) => i == worldFeedList.SelectedIndex).ToList()
            : shownWorldFeeds.ToList();
        var added = 0;
        foreach (var f in chosen)
        {
            if (cat.Feeds.Any(x => string.Equals(x.Url, f.Url, StringComparison.OrdinalIgnoreCase))) continue;
            cat.Feeds.Add(new FeedSource(f.Name, f.Url));
            added++;
        }
        ShowFeedsOfSelectedCategory();
        discoverStatus.Text = added == 0 ? L("msg.exists") : Loc.T("worldfeeds.added", added);
        if (added > 0) MarkDirty();
    }

    private FeedCategory? SelectedCategory() =>
        categoryList.SelectedIndex >= 0 && categoryList.SelectedIndex < workCategories.Count ? workCategories[categoryList.SelectedIndex] : null;

    private void ShowFeedsOfSelectedCategory()
    {
        feedList.ClearRows();
        if (SelectedCategory() is { } cat)
            foreach (var f in cat.Feeds) feedList.AddRow($"{f.Name}   —   {f.Url}", f.Enabled);
    }

    private void AddCategory()
    {
        var name = Prompt.Ask(this, L("prompt.newCategory"), L("prompt.name"), "");
        if (string.IsNullOrWhiteSpace(name)) return;
        var activeId = category.SelectedIndex >= 0 && category.SelectedIndex < workCategories.Count ? workCategories[category.SelectedIndex].Id : null;
        workCategories.Add(new FeedCategory { Id = "user-" + Guid.NewGuid().ToString("N")[..8], NameEl = name.Trim(), NameEn = name.Trim() });
        RebuildCategoryLists(activeId);
        categoryList.SelectedIndex = workCategories.Count - 1;
    }

    private void RenameCategory()
    {
        if (SelectedCategory() is not { } cat) return;
        var name = Prompt.Ask(this, L("prompt.rename"), L("prompt.name"), cat.DisplayName);
        if (string.IsNullOrWhiteSpace(name)) return;
        // Only the name of the language in use changes: the other language keeps its own name.
        if (Loc.IsGreek) cat.NameEl = name.Trim();
        else cat.NameEn = name.Trim();
        if (!cat.BuiltIn)
        {
            cat.NameEl = name.Trim();
            cat.NameEn = name.Trim();
        }
        var i = categoryList.SelectedIndex;
        var activeId = category.SelectedIndex >= 0 && category.SelectedIndex < workCategories.Count ? workCategories[category.SelectedIndex].Id : null;
        RebuildCategoryLists(activeId);
        categoryList.SelectedIndex = i;
    }

    private void DeleteCategory()
    {
        if (SelectedCategory() is not { } cat) return;
        if (cat.BuiltIn)
        {
            SetStatus(L("msg.builtinNoDelete"));
            return;
        }
        workCategories.Remove(cat);
        RebuildCategoryLists(workCategories.FirstOrDefault()?.Id);
    }

    private void RemoveFeed()
    {
        if (SelectedCategory() is not { } cat || feedList.SelectedIndex < 0 || feedList.SelectedIndex >= cat.Feeds.Count) return;
        cat.Feeds.RemoveAt(feedList.SelectedIndex);
        ShowFeedsOfSelectedCategory();
        MarkDirty();
    }

    private void MoveFeed(int step)
    {
        if (SelectedCategory() is not { } cat) return;
        var from = feedList.SelectedIndex;
        var to = from + step;
        if (from < 0 || from >= cat.Feeds.Count || to < 0 || to >= cat.Feeds.Count) return;
        (cat.Feeds[from], cat.Feeds[to]) = (cat.Feeds[to], cat.Feeds[from]);
        ShowFeedsOfSelectedCategory();
        feedList.SelectedIndex = to;
        MarkDirty();
    }

    /// <summary>Downloads the selected feed (or every feed of the category) and reports whether it works and how fresh it is.</summary>
    private async void TestFeeds(bool selectedOnly)
    {
        if (SelectedCategory() is not { } cat) return;
        var feeds = selectedOnly
            ? (feedList.SelectedIndex >= 0 && feedList.SelectedIndex < cat.Feeds.Count ? new List<FeedSource> { cat.Feeds[feedList.SelectedIndex] } : new List<FeedSource>())
            : cat.Feeds.ToList();
        if (feeds.Count == 0) return;
        discoverStatus.Text = Loc.T("pk.testing");
        try
        {
            using var provider = new RssNewsProvider();
            var results = await Task.WhenAll(feeds.Select(f => provider.TestAsync(f.Url))).ConfigureAwait(true);
            if (IsDisposed) return;
            if (feeds.Count == 1)
            {
                var r = results[0];
                discoverStatus.Text = r.Ok
                    ? Loc.T("feed.ok", r.Count, (r.Newest?.ToString("dd/MM HH:mm") ?? ""))
                    : Loc.T("feed.fail", r.Error ?? "");
                return;
            }
            var bad = feeds.Where((f, i) => !results[i].Ok).Select(f => f.Name).ToList();
            discoverStatus.Text = bad.Count == 0
                ? Loc.T("feed.allOk", feeds.Count)
                : Loc.T("feed.some", feeds.Count - bad.Count, feeds.Count, string.Join(", ", bad));
        }
        catch (Exception ex)
        {
            Log.Write($"feed test failed: {ex.GetType().Name}: {ex.Message}");
            if (!IsDisposed) discoverStatus.Text = ex is OperationCanceledException or InvalidOperationException ? ex.Message : Loc.T("feed.fail", ex.GetType().Name);
        }
    }

    private void ImportOpml()
    {
        if (SelectedCategory() is not { } cat) return;
        using var dialog = new OpenFileDialog { Filter = "OPML files (*.opml;*.xml)|*.opml;*.xml|All files (*.*)|*.*", Title = Loc.T("pk.import.opml.subscriptions") };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            using var input = File.OpenRead(dialog.FileName);
            var imported = OpmlExchange.Import(input);
            var before = cat.Feeds.Count;
            foreach (var feed in imported) AddFeed(cat, feed.Name, feed.Url);
            ShowFeedsOfSelectedCategory();
            discoverStatus.Text = Loc.T("opml.imported", cat.Feeds.Count - before, imported.Count);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException or InvalidDataException)
        {
            MessageBox.Show(this, ex.Message, Loc.T("pk.opml.import.failed"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ExportOpml()
    {
        if (SelectedCategory() is not { } cat) return;
        using var dialog = new SaveFileDialog { Filter = "OPML files (*.opml)|*.opml|XML files (*.xml)|*.xml", DefaultExt = "opml", AddExtension = true, FileName = $"{cat.Id}.opml", Title = Loc.T("pk.export.opml.subscriptions") };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            using var output = File.Create(dialog.FileName);
            OpmlExchange.Export(cat.Feeds, output, cat.DisplayName);
            discoverStatus.Text = Loc.T("pk.opml.export.completed");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            MessageBox.Show(this, ex.Message, Loc.T("pk.opml.export.failed"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void AddPreset()
    {
        if (SelectedCategory() is not { } cat || presets.SelectedIndex < 0) return;
        var p = AppConfig.PresetFeeds[presets.SelectedIndex];
        AddFeed(cat, p.Name, p.Url);
    }

    private async void AddFromSite()
    {
        if (SelectedCategory() is not { } cat) return;
        var text = siteBox.Value.Trim();
        if (text.Length == 0) return;
        discoverStatus.Text = L("msg.searching");
        siteBox.Enabled = false;
        try
        {
            var found = await FeedDiscovery.DiscoverAsync(text);
            if (IsDisposed) return;
            if (found.Count == 0)
            {
                discoverStatus.Text = L("msg.noRss");
                return;
            }
            foreach (var f in found.Take(3)) AddFeed(cat, f.Name, f.Url);
            discoverStatus.Text = Loc.T("msg.added", Math.Min(3, found.Count));
            siteBox.Value = "";
        }
        catch (Exception ex)
        {
            // an async void handler must never let an exception out: it would end the program
            Log.Write($"add feed from site failed: {ex.GetType().Name}: {ex.Message}");
            if (!IsDisposed) discoverStatus.Text = L("msg.noRss");
        }
        finally
        {
            if (!IsDisposed) siteBox.Enabled = true;
        }
    }

    private void AddFeed(FeedCategory cat, string name, string url)
    {
        if (cat.Feeds.Any(f => string.Equals(f.Url, url, StringComparison.OrdinalIgnoreCase)))
        {
            discoverStatus.Text = L("msg.exists");
            return;
        }
        cat.Feeds.Add(new FeedSource(string.IsNullOrWhiteSpace(name) ? new Uri(url).Host : name, url));
        ShowFeedsOfSelectedCategory();
    }

}

/// <summary>Tiny modal "enter a text" dialog in the flat style.</summary>
internal static class Prompt
{
    public static string? Ask(Control owner, string title, string label, string initial)
    {
        using var form = new Form
        {
            Text = title, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false, MaximizeBox = false, ClientSize = new Size(420, 130), ShowInTaskbar = false,
            Font = new Font("Segoe UI", 9.5f)
        };
        var l = new Label { Text = label, AutoSize = true, Left = 16, Top = 14 };
        var box = new FlatText { Left = 16, Top = 40, Width = 388, Value = initial };
        var ok = new FlatButton { Text = Loc.T("btn.ok"), Left = 16, Top = 86, DialogResult = DialogResult.OK };
        ok.SetPrimary(true);
        var cancel = new FlatButton { Text = Loc.T("btn.cancel"), Left = 110, Top = 86, DialogResult = DialogResult.Cancel };
        form.Controls.AddRange(new Control[] { l, box, ok, cancel });
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        form.BackColor = Theme.Background;
        form.ForeColor = Theme.Text;
        Theme.Apply(form);
        form.HandleCreated += (_, _) => Theme.ApplyTitleBar(form);
        form.Shown += (_, _) => box.FocusInner();
        return form.ShowDialog(owner.FindForm()) == DialogResult.OK ? box.Value : null;
    }
}
