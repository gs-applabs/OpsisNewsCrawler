using OpsisNewsCrawler.Core;
using OpsisNewsCrawler.Plugin.Abstractions;
using System.Diagnostics;

namespace OpsisNewsCrawler.Windows;

/// <summary>
/// Owns the bars (the main one, and optionally a second one inside the taskbar), their state, the settings and the
/// news / quotes refresh loops. Used by the standalone app and by the Opsis plugin. Create and use it on the UI thread.
/// </summary>
public sealed partial class TickerHost : IDisposable
{
    private static readonly TimeSpan MinRefreshGap = TimeSpan.FromSeconds(30);

    private readonly string configPath;
    private readonly Func<IntPtr, bool>? hostWindowDetector;
    private readonly RssNewsProvider news = new();
    private readonly QuoteService quotes = new();
    private readonly System.Windows.Forms.Timer refreshTimer = new();
    private readonly System.Windows.Forms.Timer quoteTimer = new();
    private readonly System.Windows.Forms.Timer snoozeTimer = new();
    private bool snoozed;
    private readonly CancellationTokenSource cts = new();
    private readonly CancellationToken lifetimeToken;
    private readonly RefreshScheduler refreshScheduler = new();
    private readonly ReadStore readStore = new();

    /// <summary>What the user taught the program about urgent news ("not breaking", muted source / topic); local only.</summary>
    private readonly BreakingFeedback breakingFeedback = BreakingFeedback.Load();

    /// <summary>Articles kept with Ctrl+click.</summary>
    public ReadLaterStore ReadLater { get; } = new();
    private List<NewsItem> allItems = new();
    private IReadOnlyList<NewsItem> stockItems = Array.Empty<NewsItem>();
    private bool refreshing;
    private bool refreshingQuotes;
    private bool pendingForced;
    private DateTimeOffset? newsDataAt;
    private List<string> newsFailedFeeds = new();
    private bool hasRealItems;
    private bool hasQuotes;
    private bool disposed;
    private bool started;
    private int startEpoch; // discard delayed checks from a stopped session

    // breaking news
    private readonly System.Windows.Forms.Timer breakingTimer = new() { Interval = 5000 };
    private List<NewsItem> breakingItems = new();
    private bool breakingActive;
    private DateTimeOffset breakingUntil;
    private LivePreviewForm? livePreview;
    private string? breakingVideoContext;

    public AppConfig Config { get; }

    /// <summary>One in-memory-only snapshot for a host desktop widget; no network requests.</summary>
    public IReadOnlyList<NewsItem> WidgetSnapshot(string widget) => widget switch
    {
        "news" => allItems.Where(x => x.Kind == NewsItemKind.News).Take(12).ToArray(),
        "markets" => stockItems.Where(x => x.Kind == NewsItemKind.Quote).Take(12).ToArray(),
        "weather" => WeatherForDisplay().Where(x => x.Kind == NewsItemKind.Weather).Take(8).ToArray(),
        "breaking" => breakingItems.Take(10).ToArray(),
        "history" => OpsisNewsCrawler.Plugin.Abstractions.PluginFeeds.AllRich().FirstOrDefault() is { } rich
            ? PluginFeedForDisplay(rich.Key).Take(12).ToArray() : Array.Empty<NewsItem>(),
        _ => Array.Empty<NewsItem>()
    };

    /// <summary>Scroll state of the main bar.</summary>
    public TickerState State { get; } = new();

    /// <summary>Scroll state of the second bar (inside the taskbar).</summary>
    public TickerState State2 { get; } = new();

    /// <summary>Number of periodic refresh operations currently executing.</summary>
    public int RunningRefreshes => refreshScheduler.RunningCount;
    /// <summary>Successful RSS payload bytes; local process counter (not an upload).</summary>
    public long NetworkReceivedBytes => news.DownloadedBytes + extraNewsProvider.DownloadedBytes;

    /// <summary>The main bar.</summary>
    public TickerWindow Window { get; }

    /// <summary>The second bar, inside the taskbar; null while it is switched off.</summary>
    public TickerWindow? SecondWindow { get; private set; }

    // One extra bar per other monitor (settings "Monitors"), each with its own scroll state.
    private readonly Dictionary<string, (TickerWindow Window, TickerState State)> monitorWindows = new();

    private IEnumerable<TickerWindow> ExtraWindows => monitorWindows.Values.Select(v => v.Window);

    /// <summary>A newer release found by the last check (automatic or "Check now"), or null.</summary>
    public UpdateInfo? AvailableUpdate { get; private set; }

    /// <summary>Raised on the UI thread when an update check found a newer release.</summary>
    public event Action<UpdateInfo>? UpdateFound;

    /// <summary>Asks GitHub about a newer release. Returns null for "up to date", throws only network exceptions.</summary>
    public async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        var current = Version.TryParse(AboutTexts.Version, out var v) ? v : new Version(0, 0);
        var found = await UpdateChecker.CheckAsync(current, lifetimeToken);
        Config.LastUpdateCheck = DateTimeOffset.Now;
        SaveConfig();
        AvailableUpdate = found;
        if (found is not null && !disposed)
        {
            void Raise() => UpdateFound?.Invoke(found);
            if (Window.IsHandleCreated && Window.InvokeRequired) Window.BeginInvoke(new Action(Raise)); else Raise();
        }
        return found;
    }

    private readonly System.Windows.Forms.Timer updateTimer = new() { Interval = (int)TimeSpan.FromHours(24).TotalMilliseconds };
    private bool autoChecking;

    /// <summary>Raised (on the UI thread) once per version when an automatic check finds a newer release: the tray shows a notification.</summary>
    public event Action<UpdateInfo>? UpdateAnnounce;

    /// <summary>Raised after an update setup was handed to its helper: the program must end now so the installer can replace its files.</summary>
    public event Action? ExitRequested;

    public void RequestExit() => ExitRequested?.Invoke();

    /// <summary>About 30 seconds after the start and then every 24 hours (GearWin's logic); while offline it asks again after a minute, up to four times.</summary>
    private async Task AutoCheckForUpdatesAsync()
    {
        if (!Config.CheckForUpdates || autoChecking || disposed) return;
        autoChecking = true;
        try
        {
            for (var attempt = 0; attempt < 4 && !disposed; attempt++)
            {
                try
                {
                    var found = await CheckForUpdatesAsync();
                    // Inside Opsis the balloon is Opsis' job (the card in the settings still shows the update); never while a full-screen app is in front.
                    if (found is not null && !HostedByOpsis && UpdateAnnouncePolicy.ShouldAnnounce(Config.LastNotifiedUpdate, found.Tag))
                    {
                        if (Window.IsHandleCreated && Native.IsFullscreenAppActive(Window.Handle, hostWindowDetector))
                        {
                            _ = RetryUpdateAnnounceLater();
                            return;
                        }
                        Config.LastNotifiedUpdate = found.Tag;
                        SaveConfig();
                        if (Window.IsHandleCreated) Window.BeginInvoke(new Action(() => UpdateAnnounce?.Invoke(found)));
                    }
                    return;
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                {
                    if (disposed || cts.IsCancellationRequested) return;
                    await Task.Delay(TimeSpan.FromMinutes(1), lifetimeToken).ConfigureAwait(true);   // offline: wait for the network
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            autoChecking = false;
        }
    }

    private async Task RetryUpdateAnnounceLater()
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(15), lifetimeToken).ConfigureAwait(true);
            _ = AutoCheckForUpdatesAsync();
        }
        catch (OperationCanceledException) { }
    }

    private async Task FirstUpdateCheckAsync(int epoch)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), lifetimeToken).ConfigureAwait(true);
            if (started && !disposed && startEpoch == epoch) _ = AutoCheckForUpdatesAsync();
        }
        catch (OperationCanceledException) { }
    }

    public string ConfigPath => configPath;

    /// <summary>The folder of the settings file: the catalogue cache of the Opsis family window lives there too.</summary>
    public string DataDirectory => Path.GetDirectoryName(Path.GetFullPath(configPath)) ?? ".";

    // ---- welcome guide and the Opsis family window --------------------------------------------------------

    /// <summary>Where a closing splash shrinks to. The standalone app points it at the settings window's header icon when that is open; otherwise the tray area.</summary>
    public Func<Rectangle>? MinimiseTargetProvider { get; set; }

    private Rectangle MinimiseTargetRect() => (MinimiseTargetProvider ?? MinimiseTarget.Tray)();

    private WelcomeForm? welcome;
    private SuiteForm? suite;

    private static Version CurrentVersion => Version.TryParse(AboutTexts.Version, out var v) ? v : new Version(0, 0);

    /// <summary>First start of a new major/minor version, not switched off, not inside Opsis (Opsis has its own guide).</summary>
    public bool ShouldShowWelcome() => WelcomePolicy.ShouldShow(CurrentVersion, Config.WelcomeSeenVersion, Config.WelcomeDisabled, HostedByOpsis);

    /// <summary>Opens the welcome guide (also from the tray menu). Closing remembers the version; "show the family afterwards" opens the family window once.</summary>
    public void ShowWelcome()
    {
        if (disposed) return;
        if (welcome is { IsDisposed: false }) { welcome.Activate(); return; }
        var form = new WelcomeForm(MinimiseTargetRect) { };
        form.SetDontShowAgain(Config.WelcomeDisabled);
        welcome = form;
        form.FormClosed += (_, _) =>
        {
            welcome = null;
            var v = CurrentVersion;
            var seen = $"{v.Major}.{v.Minor}.0";
            Config.WelcomeSeenVersion = seen;
            Config.WelcomeDisabled = form.DontShowAgain;
            var offer = form.ShowFamilyAfterwards && Config.SuiteOfferedVersion != seen;
            if (offer) Config.SuiteOfferedVersion = seen;
            SaveConfig();
            if (!offer || disposed) return;
            var later = new System.Windows.Forms.Timer { Interval = 350 };
            later.Tick += (_, _) => { later.Stop(); later.Dispose(); ShowSuite(); };
            later.Start();
        };
        form.Show();
        form.Activate();
    }

    /// <summary>Closes the welcome guide the way the user's Esc would (with the shrink animation). For the test switch.</summary>
    public void CloseWelcome() => welcome?.Close();

    /// <summary>Opens the "Opsis family" window (one at a time).</summary>
    public void ShowSuite()
    {
        if (disposed) return;
        if (suite is { IsDisposed: false }) { suite.Activate(); return; }
        var form = new SuiteForm(DataDirectory, MinimiseTargetRect);
        suite = form;
        form.FormClosed += (_, _) => suite = null;
        form.Show();
        form.Activate();
    }

    /// <summary>The headlines loaded for the active category (a copy; UI thread).</summary>
    public IReadOnlyList<NewsItem> RecentNews => allItems.ToList();

    /// <summary>Combined RSS feed health for the opt-in diagnostics window.</summary>
    public IReadOnlyList<FeedHealth> FeedHealth => news.Health().Concat(extraNewsProvider.Health())
        .GroupBy(h => h.Url, StringComparer.OrdinalIgnoreCase)
        .Select(g => g.OrderByDescending(h => h.LastTry).First())
        .OrderBy(h => h.Url, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Raised when the user right-clicks a bar. The standalone app opens its window, the plugin asks Opsis to show its tab.</summary>
    public event Action? SettingsRequested;

    public TickerHost(string? configPath = null, Func<IntPtr, bool>? hostWindowDetector = null)
    {
        this.configPath = configPath ?? AppConfig.DefaultPath;
        this.hostWindowDetector = hostWindowDetector;
        lifetimeToken = cts.Token;
        OpsisNewsCrawler.Plugin.Abstractions.PluginFeeds.Changed += OnPluginFeedsChanged;
        Config = AppConfig.Load(this.configPath);

        State.SetMode(Config.Mode);
        Theme.SetMode(Config.ThemeMode, Config.OpacityPercent / 100.0, Config.ThemeFollowAccent);
        Loc.SetLanguage(Config.Language);

        LoadCachedNews();
        LoadCachedWeather();

        Window = new TickerWindow(State, hostWindowDetector);
        WireWindow(Window);
        Window.PlacementEdited += b =>
        {
            Config.CustomX = b.X;
            Config.CustomY = b.Y;
            Config.CustomWidth = b.Width;
            SaveConfig();
            CustomBoundsChanged?.Invoke();
        };

        refreshTimer.Tick += (_, _) =>
        {
            // While another program is in front the ticker is frozen; it refreshes when the desktop is back.
            if (!Window.IsPausedByForeground) refreshScheduler.TryRun("news", () => RefreshAsync(force: false), lifetimeToken);
        };
        quoteTimer.Tick += (_, _) =>
        {
            // Re-evaluate the interval as the exchange session opens/closes.
            quoteTimer.Interval = PerformanceProfile.Current.Scaled(GetQuoteRefreshIntervalMilliseconds() / 1000) * 1000;
            if (!Window.IsPausedByForeground) refreshScheduler.TryRun("quotes", () => RefreshQuotesAsync(), lifetimeToken);
        };
        Loc.Changed += OnLanguageChanged;
        weatherTimer.Tick += (_, _) => refreshScheduler.TryRun("weather", RefreshWeatherAsync, lifetimeToken);
        hazardTimer.Tick += (_, _) => refreshScheduler.TryRun("hazards", RefreshHazardsAsync, lifetimeToken);
        alertTimer.Tick += (_, _) => refreshScheduler.TryRun("weather-alerts", CheckWeatherAlertsAsync, lifetimeToken);
        snoozeTimer.Tick += (_, _) =>
        {
            snoozeTimer.Stop();
            if (disposed || !started || !Config.BarsEnabled) return;
            snoozed = false;
            StartBars();
            SnoozeChanged?.Invoke(false);
        };
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaysChanged;
        Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerModeChanged;
        Window.LiveRequested += ToggleLivePreview;
        breakingTimer.Tick += (_, _) =>
        {
            if (breakingActive && DateTimeOffset.Now > breakingUntil) EndBreaking();
        };

        ApplyConfig();
    }

    private void WireWindow(TickerWindow w)
    {
        w.HiddenByTaskbarChanged += () =>
        {
            var hidden = Window.HiddenByTaskbar || SecondWindow?.HiddenByTaskbar == true;
            if (hidden == taskbarHidden) return;
            taskbarHidden = hidden;
            UpdateMiniWeather();
        };
        w.RefreshRequested += () =>
        {
            refreshScheduler.TryRun("news", () => RefreshAsync(force: false), lifetimeToken);
            refreshScheduler.TryRun("quotes", () => RefreshQuotesAsync(), lifetimeToken);
        };
        w.ItemOpened += item =>
        {
            if (item.Kind != NewsItemKind.News) return;
            if (!string.IsNullOrEmpty(item.Url)) readStore.MarkRead(item.Url);
            if (breakingActive)
            {
                // Opening an urgent headline acknowledges the alert: ALL current urgent items are marked as seen and the bar goes back to normal.
                // (Before, the bar stayed red and showed nothing but the other urgent items - e.g. the same story from several sources - until the timeout.)
                foreach (var urgent in breakingItems.Where(i => !string.IsNullOrEmpty(i.Url))) readStore.MarkRead(urgent.Url);
                EndBreaking();
                return;
            }
            if (Config.HideRead && allItems.Count > 0) PushAll();
        };
        w.SettingsRequested += () => SettingsRequested?.Invoke();
        w.WeatherRequested += ShowWeatherMap;
        w.HazardRequested += ShowHazardDetail;
        w.ItemSaveRequested += item =>
        {
            var added = ReadLater.Add(item);
            w.ShowToast(Loc.T(added ? "msg.savedLater" : "msg.alreadySaved"));
        };
        w.ItemMarkReadRequested += item =>
        {
            readStore.MarkRead(item.Url);
            if (breakingActive)
            {
                breakingItems.RemoveAll(i => string.Equals(i.Url, item.Url, StringComparison.OrdinalIgnoreCase));
                if (breakingItems.Count == 0) EndBreaking();
            }
            if (Config.HideRead) PushAll();
            w.ShowToast(Loc.T("pk.marked.as.read"));
        };
        w.BreakingFeedbackRequested += (item, kind) =>
        {
            switch (kind)
            {
                case BreakingFeedbackKind.NotBreaking: breakingFeedback.Reject(item); break;
                case BreakingFeedbackKind.MuteSource: breakingFeedback.MuteSource(item); break;
                case BreakingFeedbackKind.MuteTopic: breakingFeedback.MuteTopic(item); break;
            }
            breakingFeedback.Save();
            // the urgent state is recomputed at once: the story (or the source) leaves the red bar
            breakingItems.RemoveAll(i => !string.IsNullOrEmpty(i.Url) && (string.Equals(i.Url, item.Url, StringComparison.OrdinalIgnoreCase)
                || kind == BreakingFeedbackKind.MuteSource && BreakingEngine.Publisher(i) == BreakingEngine.Publisher(item)));
            if (breakingActive && breakingItems.Count == 0) EndBreaking();
            DetectBreaking(allItems);
            PushAll();
            w.ShowToast(Loc.T("breaking.feedbackSaved"));
        };
        // Every bar has its own speed (and so has the second half of a split bar): the wheel changes only the bar it is turned over.
        w.ScrollSpeedChanged += speed => { SetBarSpeed(w, false, (int)Math.Round(speed)); };
        w.ScrollSpeedBChanged += speed => { SetBarSpeed(w, true, (int)Math.Round(speed)); };
    }

    /// <summary>The two speeds of a bar from the settings (0 = like the main bar).</summary>
    private (int A, int B) SpeedsOf(TickerWindow w)
    {
        int Or(int own) => own > 0 ? own : Config.ScrollSpeed;
        if (ReferenceEquals(w, Window)) return (Config.ScrollSpeed, Config.MainSpeedB);
        if (ReferenceEquals(w, SecondWindow)) return (Or(Config.SecondSpeed), Config.SecondSpeedB);
        if (ReferenceEquals(w, miniWeather)) return (Or(Config.MiniSpeed), Config.MiniSpeedB);
        return (Or(Config.MonitorSpeed), Config.MonitorSpeedB);
    }

    /// <summary>Font, text size and (where the bar is free to have one) height of a bar, from the settings of that bar.</summary>
    private void ApplyTextAndSize(TickerWindow w, TickerPosition position)
    {
        var main = ReferenceEquals(w, Window);
        var second = ReferenceEquals(w, SecondWindow);
        var mini = ReferenceEquals(w, miniWeather);
        var (family, percent) = main ? (Config.MainFontFamily, Config.MainFontPercent)
            : second ? (Config.SecondFontFamily, Config.SecondFontPercent)
            : mini ? (Config.MiniFontFamily, Config.MiniFontPercent)
            : (Config.MonitorFontFamily, Config.MonitorFontPercent);
        w.SetTextStyle(family, percent);
        // The desktop bar has its own height setting; a bar inside the taskbar takes the height of the taskbar.
        if (mini || second || position == TickerPosition.TaskbarIntegrated) return;
        var height = main ? Config.MainBarHeight : Config.MonitorBarHeight;
        w.SetBarHeight(height > 0 ? height : 42);
    }

    private void ApplySpeeds(TickerWindow w)
    {
        var (a, b) = SpeedsOf(w);
        w.ScrollSpeed = a;
        w.ScrollSpeedB = b;
    }

    private void SetBarSpeed(TickerWindow w, bool secondHalf, int value)
    {
        value = AppConfig.CleanSpeed(value);
        if (ReferenceEquals(w, Window)) { if (secondHalf) Config.MainSpeedB = value; else Config.ScrollSpeed = Math.Max(AppConfig.MinScrollSpeed, value); }
        else if (ReferenceEquals(w, SecondWindow)) { if (secondHalf) Config.SecondSpeedB = value; else Config.SecondSpeed = value; }
        else if (ReferenceEquals(w, miniWeather)) { if (secondHalf) Config.MiniSpeedB = value; else Config.MiniSpeed = value; }
        else { if (secondHalf) Config.MonitorSpeedB = value; else Config.MonitorSpeed = value; }
        ApplySpeeds(Window);
        if (SecondWindow is not null) ApplySpeeds(SecondWindow);
        if (miniWeather is not null) ApplySpeeds(miniWeather);
        foreach (var extra in ExtraWindows) ApplySpeeds(extra);
        SaveConfig();
    }

    private void OnLanguageChanged()
    {
        // Texts built from the language (placeholders, quote labels) are rebuilt.
        if (started) _ = RefreshQuotesAsync(force: true);
        PushAll();
        UpdateMiniWeather();
    }

    /// <summary>Set by the Opsis plugin: the UI language then follows Opsis, not the crawler's own setting.</summary>
    public bool LanguageFollowsHost { get; set; }

    /// <summary>Pushes the values of <see cref="Config"/> into the windows and timers. Does not save.</summary>
    private GlobalHotkey? hotkey;

    /// <summary>Registers or removes the Ctrl+Alt+N hot key that shows / hides the bars.</summary>
    private void UpdateHotkey()
    {
        var want = Config.ToggleHotkey && !HostedByOpsis && !disposed;
        if (want && hotkey is null)
        {
            hotkey = new GlobalHotkey();
            hotkey.Pressed += () => SetBarsEnabled(!Config.BarsEnabled);
            if (!hotkey.Register(GlobalHotkey.ModControl | GlobalHotkey.ModAlt, 0x4E))
                Log.Write("Hot key Ctrl+Alt+N could not be registered (another program uses it).");
        }
        else if (!want && hotkey is not null)
        {
            hotkey.Dispose();
            hotkey = null;
        }
    }

    public void ApplyConfig()
    {
        YahooAccess.Enabled = Config.HistoryFromYahoo;
        if (!LanguageFollowsHost) Loc.SetLanguage(Config.Language);
        Theme.SetMode(Config.ThemeMode, Config.OpacityPercent / 100.0, Config.ThemeFollowAccent);
        UpdateHotkey();

        ConfigureWindow(Window, Config.Position, main: true);
        Window.SetBackdropStyle(Config.BarBackdrop);
        Window.SetBarColor(Config.BarColorMode, Config.BarCustomColor);
        Window.ReserveSpace = EffectiveReserveSpace;
        Window.SetCustomBounds(GetCustomBounds());
        Window.SetPosition(Config.Position);

        var wantSecond = Config.SecondBarEnabled && Config.Position != TickerPosition.TaskbarIntegrated;
        if (wantSecond && SecondWindow is null)
        {
            SecondWindow = new TickerWindow(State2, hostWindowDetector);
            WireWindow(SecondWindow);
            if (BarsOn) SecondWindow.StartTicker();
        }
        else if (!wantSecond && SecondWindow is not null)
        {
            SecondWindow.StopTicker();
            SecondWindow.Dispose();
            SecondWindow = null;
        }
        if (SecondWindow is not null)
        {
            ConfigureWindow(SecondWindow, TickerPosition.TaskbarIntegrated, main: false);
            SecondWindow.SetBackdropStyle(Config.TaskbarBarBackdrop);
            SecondWindow.SetBarColor(Config.TaskbarBarColorMode, Config.TaskbarBarCustomColor);
            SecondWindow.ReserveSpace = false;
            SecondWindow.SetPosition(TickerPosition.TaskbarIntegrated);
        }

        SyncMonitorWindows();

        EvaluatePerformance();                      // the setting "Performance" may have changed
        ApplyRefreshIntervals();
        PushAll();
        if (BarsOn)
        {
            _ = RefreshQuotesAsync();
            _ = RefreshWeatherAsync();
            _ = RefreshExtraNewsAsync();
            UpdateMiniWeather();
        }
    }

    private int GetQuoteRefreshIntervalMilliseconds() =>
        QuoteRefreshPolicy.GetIntervalSeconds(Config.Watchlist, Config.QuoteRefreshSeconds, DateTimeOffset.Now) * 1000;

    /// <summary>Screens that currently get an extra bar: enabled in the settings, still connected and not the primary one.</summary>
    private IEnumerable<(MonitorBar Cfg, Screen Screen)> WantedMonitors() =>
        Config.MonitorBars.Where(m => m.Enabled)
            .Select(m => (Cfg: m, Screen: Screen.AllScreens.FirstOrDefault(s => string.Equals(s.DeviceName, m.Device, StringComparison.OrdinalIgnoreCase))))
            .Where(x => x.Screen is { Primary: false })
            .Select(x => (x.Cfg, x.Screen!));

    private void SyncMonitorWindows()
    {
        var wanted = WantedMonitors().ToDictionary(x => x.Cfg.Device, StringComparer.OrdinalIgnoreCase);
        foreach (var gone in monitorWindows.Keys.Where(k => !wanted.ContainsKey(k)).ToList())
        {
            var (w, _) = monitorWindows[gone];
            w.StopTicker();
            w.Dispose();
            monitorWindows.Remove(gone);
        }
        foreach (var (device, x) in wanted)
        {
            if (!monitorWindows.TryGetValue(device, out var entry))
            {
                var state = new TickerState();
                state.SetMode(Config.Mode);
                var w = new TickerWindow(state, hostWindowDetector) { TargetScreen = x.Screen };
                WireWindow(w);
                entry = (w, state);
                monitorWindows[device] = entry;
                ConfigureWindow(w, TickerPosition.BottomTaskbar, main: false);
                w.FollowTaskbar = true;
                w.ReserveSpace = false;
                w.SetBackdropStyle(Config.BarBackdrop);
                w.SetBarColor(Config.BarColorMode, Config.BarCustomColor);
                w.SetPosition(TickerPosition.BottomTaskbar);
                if (BarsOn) w.StartTicker();
            }
            else
            {
                entry.Window.TargetScreen = x.Screen; // the screen may have moved or changed size
                ConfigureWindow(entry.Window, TickerPosition.BottomTaskbar, main: false);
                entry.Window.FollowTaskbar = true;
                entry.Window.ReserveSpace = false;
                entry.Window.SetBackdropStyle(Config.BarBackdrop);
                entry.Window.SetBarColor(Config.BarColorMode, Config.BarCustomColor);
                entry.Window.SetPosition(TickerPosition.BottomTaskbar);
            }
        }
    }

    private void OnPowerModeChanged(object? sender, Microsoft.Win32.PowerModeChangedEventArgs e)
    {
        // After sleep the data is hours old and the network needs a few seconds to come back: refresh everything once it has.
        if (e.Mode != Microsoft.Win32.PowerModes.Resume || disposed || !Window.IsHandleCreated) return;
        try
        {
            Window.BeginInvoke(new Action(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(6));
                if (disposed || !started) return;
                _ = RefreshAsync(force: true);
                _ = RefreshQuotesAsync();
                _ = RefreshWeatherAsync();
            }));
        }
        catch (InvalidOperationException) { }
    }

    private void OnDisplaysChanged(object? sender, EventArgs e)
    {
        // A monitor was connected, disconnected or rearranged: add, remove and move the extra bars.
        if (disposed || !Window.IsHandleCreated) return;
        try { Window.BeginInvoke(new Action(() => { if (!disposed) { SyncMonitorWindows(); PushAll(); } })); }
        catch (InvalidOperationException) { }
    }

    private void ConfigureWindow(TickerWindow w, TickerPosition position, bool main)
    {
        w.PauseOnForeground = Config.PauseOnOtherWindow;
        w.FollowTaskbar = Config.FollowTaskbar;
        w.PauseOnHover = Config.PauseOnHover;
        w.ShowPreview = Config.ShowPreview;
        w.ShowIcons = Config.ShowIcons;
        ApplySpeeds(w);
        ApplyTextAndSize(w, position);
        w.TextShadow = Config.TextShadow;
        w.AutoText = !string.Equals(Config.TextColorMode, "fixed", StringComparison.OrdinalIgnoreCase);
        w.SetThemeModeName(Config.ThemeMode);
        w.EdgeFade = Config.EdgeFade;
        w.SetShape(Config.BarShape);
        w.TaskbarAutoDetect = Config.TaskbarAutoDetect;
        w.TaskbarLeftOffset = Config.TaskbarLeftOffset;
        w.TaskbarRightOffset = Config.TaskbarRightOffset;
    }

    /// <summary>The custom strip in screen coordinates; a first-time default sits at the bottom of the primary screen.</summary>
    public Rectangle GetCustomBounds()
    {
        if (Config.CustomWidth >= 200) return new Rectangle(Config.CustomX, Config.CustomY, Config.CustomWidth, 42);
        var wa = (Screen.PrimaryScreen ?? Screen.AllScreens[0]).WorkingArea;
        return new Rectangle(wa.Left + 100, wa.Bottom - 42 - 40, Math.Min(900, wa.Width - 200), 42);
    }

    /// <summary>Stores and applies a custom strip given in screen coordinates (width at least 200).</summary>
    public void SetCustomBounds(int x, int y, int width)
    {
        Config.CustomX = x;
        Config.CustomY = y;
        Config.CustomWidth = Math.Max(200, width);
        Window.SetCustomBounds(GetCustomBounds());
        SaveConfig();
    }

    /// <summary>Lets the user move/resize the strip with the mouse (Custom position only).</summary>
    public void SetPositionEditMode(bool on)
    {
        if (on && Config.Position != TickerPosition.Custom) SetPosition(TickerPosition.Custom);
        Window.SetEditMode(on);
    }

    public bool IsPositionEditMode => Window.IsEditMode;

    /// <summary>Raised after the user dragged/resized the strip with the mouse, so a settings page can refresh its boxes.</summary>
    public event Action? CustomBoundsChanged;

    /// <summary>Set by the Opsis plugin before the first ApplyConfig: Opsis widgets, DeskStrip and DeskZones follow the work area.</summary>
    public bool HostedByOpsis { get; set; }

    public bool EffectiveReserveSpace => Config.ReserveScreenSpace ?? HostedByOpsis;

    public void SaveConfig() => Config.Save(configPath);

    private bool BarsOn => started && Config.BarsEnabled && !snoozed;

    // ---- performance tier (Low / Balanced / High) -------------------------------------------------------

    private readonly PerformanceController performance = new(HardwareDetector.Detect);
    private readonly System.Windows.Forms.Timer performanceTimer = new() { Interval = 60_000 };
    private PerformanceTier? hostTier;

    /// <summary>What was detected and the tier in use (for the settings page).</summary>
    public PerformanceController Performance => performance;

    /// <summary>Inside Opsis: the tier Opsis reports ("Low" / "Balanced" / "High"); it decides while the setting is Automatic.</summary>
    public void SetHostTier(string? tier)
    {
        hostTier = PerformanceClassifier.ParseTier(tier);
        EvaluatePerformance();
    }

    /// <summary>
    /// One evaluation of the tier (test variable OPSISNEWSCRAWLER_FORCE_TIER, then the setting, then the host, then the detection) and, when it changed,
    /// the new profile applies everywhere at once. Runs at the start, when the settings change and every minute (going up needs two equal readings).
    /// </summary>
    public void EvaluatePerformance()
    {
        if (disposed) return;
        var forced = PerformanceClassifier.ParseTier(Environment.GetEnvironmentVariable("OPSISNEWSCRAWLER_FORCE_TIER"));
        if (performance.Evaluate(PerformanceClassifier.ParseMode(Config.PerformanceMode), hostTier, forced)) ApplyPerformance();
    }

    private void ApplyPerformance()
    {
        var profile = PerformanceProfile.For(performance.Tier);
        PerformanceProfile.Current = profile;
        TickerWindow.LowPower = profile.Tier == PerformanceTier.Low;
        TickerWindow.RaisePerformanceChanged();
        ApplyRefreshIntervals();
        Log.Write($"Performance tier: {profile.Tier} ({performance.Reason}); specs: {(performance.Specs is { } s ? $"{s.LogicalCores} processors, {s.RamGb} GB, GPU {s.HasGpu}, battery {s.OnBattery}, saver {s.BatterySaver}" : "not measured")}");
        PerformanceChanged?.Invoke();
    }

    /// <summary>Raised on the UI thread after the tier changed (the settings page shows the new one).</summary>
    public event Action? PerformanceChanged;

    /// <summary>The timers of the refreshes follow the user's intervals times the factor of the tier (2 on a low PC, 1 otherwise).</summary>
    public EnergyRefreshBudget CurrentEnergyBudget =>
        EnergyRefreshBudget.For(performance.Specs, Config.NetworkCostMode == "metered", PerformanceProfile.Current.Tier);

    private void ApplyRefreshIntervals()
    {
        var p = PerformanceProfile.Current;
        var budget = CurrentEnergyBudget;
        refreshTimer.Interval = budget.RefreshMs(p.Scaled(Config.RefreshIntervalSeconds));
        quoteTimer.Interval = budget.RefreshMs(p.Scaled(GetQuoteRefreshIntervalMilliseconds() / 1000));
        weatherTimer.Interval = budget.RefreshMs(p.Scaled(15 * 60));
        hazardTimer.Interval = budget.RefreshMs(p.Scaled(Math.Max(1, Config.HazardRefreshMinutes) * 60));
    }

    public void Start()
    {
        if (disposed || started) return;
        started = true;
        unchecked { startEpoch++; }
        EvaluatePerformance();
        performanceTimer.Tick -= OnPerformanceTimer;
        performanceTimer.Tick += OnPerformanceTimer;
        performanceTimer.Start();
        PushAll();
        if (Config.BarsEnabled) StartBars();
        _ = FirstUpdateCheckAsync(startEpoch);
        updateTimer.Tick -= OnUpdateTimer;
        updateTimer.Tick += OnUpdateTimer;
        updateTimer.Start();
    }

    private void OnUpdateTimer(object? sender, EventArgs e) => _ = AutoCheckForUpdatesAsync();

    private void OnPerformanceTimer(object? sender, EventArgs e) => EvaluatePerformance();

    private void StartBars()
    {
        Window.StartTicker();
        SecondWindow?.StartTicker();
        foreach (var extra in ExtraWindows) extra.StartTicker();
        refreshTimer.Start();
        quoteTimer.Start();
        weatherTimer.Start();
        hazardTimer.Interval = CurrentEnergyBudget.RefreshMs(
            PerformanceProfile.Current.Scaled(Config.HazardRefreshMinutes * 60));
        hazardTimer.Start();
        alertTimer.Start();
        breakingTimer.Start();
        refreshScheduler.TryRun("news", () => RefreshAsync(force: true), lifetimeToken);
        refreshScheduler.TryRun("quotes", () => RefreshQuotesAsync(), lifetimeToken);
        refreshScheduler.TryRun("weather", RefreshWeatherAsync, lifetimeToken);
        refreshScheduler.TryRun("hazards", RefreshHazardsAsync, lifetimeToken);
        refreshScheduler.TryRun("weather-alerts", CheckWeatherAlertsAsync, lifetimeToken);
        UpdateMiniWeather();
    }

    private void StopBars()
    {
        breakingTimer.Stop();
        CloseLivePreview();
        weatherMap?.Close();
        weatherMap = null;
        hazardDetail?.Close();
        hazardDetail = null;
        refreshTimer.Stop();
        quoteTimer.Stop();
        weatherTimer.Stop();
        hazardTimer.Stop();
        alertTimer.Stop();
        Window.StopTicker();
        SecondWindow?.StopTicker();
        foreach (var extra in ExtraWindows) extra.StopTicker();
        UpdateMiniWeather(); // BarsOn is false now: the small bar goes away too
    }

    /// <summary>Raised after the master switch changed (tray menu and settings keep their check marks in step).</summary>
    public event Action? BarsEnabledChanged;

    /// <summary>Raised when a temporary snooze starts or ends.</summary>
    public event Action<bool>? SnoozeChanged;

    /// <summary>Temporarily hides all bars without changing the saved enabled setting.</summary>
    public void SnoozeBars(TimeSpan duration)
    {
        if (disposed || !started || !Config.BarsEnabled) return;
        var milliseconds = (int)Math.Clamp(duration.TotalMilliseconds, 1000, 60 * 60 * 1000);
        snoozeTimer.Stop();
        snoozeTimer.Interval = milliseconds;
        snoozed = true;
        StopBars();
        snoozeTimer.Start();
        SnoozeChanged?.Invoke(true);
    }

    /// <summary>Cancels a temporary snooze and restores the bars if they are enabled.</summary>
    public void CancelSnooze()
    {
        if (!snoozed) return;
        snoozeTimer.Stop();
        snoozed = false;
        if (started && Config.BarsEnabled) StartBars();
        SnoozeChanged?.Invoke(false);
    }

    /// <summary>Turns every bar on or off. Off: the bars disappear and nothing is downloaded.</summary>
    public void SetBarsEnabled(bool on)
    {
        if (Config.BarsEnabled == on) return;
        Config.BarsEnabled = on;
        var wasSnoozed = snoozed;
        snoozeTimer.Stop();
        snoozed = false;
        if (wasSnoozed) SnoozeChanged?.Invoke(false);
        SaveConfig();
        if (started)
        {
            if (on) StartBars(); else StopBars();
        }
        BarsEnabledChanged?.Invoke();
    }

    public void Stop()
    {
        if (disposed || !started) return;
        started = false;
        unchecked { startEpoch++; }
        // Plugin unload must stop *all* periodic work, not just the visible news bars.
        performanceTimer.Stop();
        updateTimer.Stop();
        snoozeTimer.Stop();
        snoozed = false;
        breakingTimer.Stop();
        CloseLivePreview();
        weatherMap?.Close();
        weatherMap = null;
        hazardDetail?.Close();
        hazardDetail = null;
        refreshTimer.Stop();
        quoteTimer.Stop();
        weatherTimer.Stop();
        hazardTimer.Stop();
        alertTimer.Stop();
        Window.StopTicker();
        SecondWindow?.StopTicker();
        foreach (var extra in ExtraWindows) extra.StopTicker();
    }

    /// <summary>True while the bars are paused from the tray menu (the scrolling stands still).</summary>
    public bool Paused { get; private set; }

    /// <summary>Raised after <see cref="Pause"/> or <see cref="Resume"/>.</summary>
    public event Action? PausedChanged;

    public void Pause()
    {
        Paused = true;
        PausedChanged?.Invoke();
        Window.PauseTicker();
        SecondWindow?.PauseTicker();
        foreach (var extra in ExtraWindows) extra.PauseTicker();
    }

    public void Resume()
    {
        Paused = false;
        PausedChanged?.Invoke();
        Window.ResumeTicker();
        SecondWindow?.ResumeTicker();
        foreach (var extra in ExtraWindows) extra.ResumeTicker();
    }

    public void Refresh()
    {
        if (disposed || !started) return;
        _ = RefreshAsync(force: true);
        _ = RefreshQuotesAsync(force: true);
    }

    public void SetMode(TickerMode mode) => SetCategory(mode.ToString());

    /// <summary>Shows another category (built-in mode name or user category id). Unknown ids are ignored.</summary>
    public void SetCategory(string id)
    {
        var cat = Config.FindCategory(id);
        if (cat is null || string.Equals(Config.ActiveCategory, cat.Id, StringComparison.OrdinalIgnoreCase)) return;
        Config.ActiveCategory = cat.Id;
        if (Enum.TryParse<TickerMode>(cat.Id, true, out var mode))
        {
            Config.Mode = mode;
            State.SetMode(mode);
        }
        SaveConfig();
        ReloadNews();
    }

    /// <summary>Call after the feed list of the active category was edited: fetch again from the start.</summary>
    public void ReloadNews()
    {
        // A different set of news: start from the beginning.
        State.SetScrollOffset(0);
        State2.SetScrollOffset(0);
        allItems = new();
        newsFailedFeeds = new();
        LoadCachedNews();
        _ = RefreshAsync(force: true);
    }

    public void SetPosition(TickerPosition position)
    {
        Config.Position = position;
        if (position == TickerPosition.TaskbarIntegrated) Config.SecondBarEnabled = false;
        Window.SetPosition(position);
        ApplyConfig();
        SaveConfig();
    }

    // ---- what the bars show ---------------------------------------------------------------

    /// <summary>What every bar that is on shows right now (main, second, monitors, desktop bar).</summary>
    private IEnumerable<BarSlots> AllBarSlots()
    {
        yield return Config.MainSlots();
        if (Config.SecondBarEnabled) yield return Config.SecondSlots();
        foreach (var (cfg, _) in WantedMonitors()) yield return cfg.Slots();
        if (Config.MiniWeatherEnabled || Config.MiniWhenTaskbarHidden) yield return Config.MiniSlots();
    }

    private bool taskbarHidden;   // a bar inside the auto-hiding taskbar is hidden right now

    /// <summary>Test switch (--test-taskbar-hidden): behaves as if the auto-hiding taskbar and the bar inside it were hidden.</summary>
    public void SimulateTaskbarHidden(bool hidden)
    {
        taskbarHidden = hidden;
        UpdateMiniWeather();
    }
    private bool MiniWanted => Config.MiniWeatherEnabled || (Config.MiniWhenTaskbarHidden && taskbarHidden);

    private bool Needs(SlotKind kind) => AllBarSlots().Any(slots => slots.Has(kind));
    private bool NeedsNews => Needs(SlotKind.News);
    private bool NeedsStocks => Needs(SlotKind.Stocks);

    private bool IsActiveCategory(string? id) => string.IsNullOrWhiteSpace(id) || string.Equals(id, Config.ActiveCategory, StringComparison.OrdinalIgnoreCase);

    /// <summary>News categories other than the active one that some bar shows (a half of a split bar, or a whole bar).</summary>
    private List<string> ExtraNewsCategories() => AllBarSlots().SelectMany(slots => slots.All())
        .Where(slot => slot.Kind == SlotKind.News && !IsActiveCategory(slot.Category))
        .Select(slot => slot.Category!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    // ---- no internet / a provider is down: the last stored data plus a message ----------------

    private static bool InternetAvailable()
    {
        try { return System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable(); }
        catch (Exception ex) when (ex is System.Net.NetworkInformation.NetworkInformationException or InvalidOperationException) { return true; }
    }

    /// <summary>A scrolling warning: no internet, or which provider does not answer, and the time of the data still shown.</summary>
    private static NewsItem? Notice(IReadOnlyCollection<string> failedProviders, DateTimeOffset? dataAt)
    {
        if (failedProviders.Count == 0) return null;
        var head = InternetAvailable() ? Loc.T("net.providerDown", string.Join(", ", failedProviders)) : Loc.T("net.offline");
        var tail = dataAt is { } at ? Loc.T("net.showingStored", at.ToLocalTime().ToString("dd/MM HH:mm")) : Loc.T("net.noStored");
        return Placeholder(head + " — " + tail);
    }

    private static IReadOnlyList<NewsItem> WithNotice(NewsItem? notice, IReadOnlyList<NewsItem> items) =>
        notice is null ? items : new[] { notice }.Concat(items).ToList();

    private void LoadCachedNews()
    {
        var cached = DataCache.Load<List<NewsItem>>("news-" + Config.ActiveCategory);
        if (cached?.Data is not { Count: > 0 } items) return;
        allItems = items;
        newsDataAt = cached.At;
        hasRealItems = true;
    }

    private void LoadCachedWeather()
    {
        var cached = DataCache.Load<List<WeatherReading>>("weather");
        if (cached?.Data is not { Count: > 0 } readings) return;
        weatherReadings = readings;
        weatherDataAt = cached.At;
        hasWeather = true;
    }

    private IReadOnlyList<NewsItem> NewsForDisplay() =>
        WithNotice(Notice(newsFailedFeeds, newsDataAt), NewsItemsOnly());

    private IReadOnlyList<NewsItem> NewsItemsOnly() => FilterNews(allItems, hasRealItems);

    private IReadOnlyList<NewsItem> FilterNews(List<NewsItem> source, bool hasReal)
    {
        if (source.Count == 0)
            return new[] { Placeholder(hasReal ? Loc.T("ph.newsUnavailable") : Loc.T("ph.loadingNews")) };
        IReadOnlyList<NewsItem> candidates = NewsKeywordFilter.Filter(source, Config.MutedNewsKeywords);
        if (Config.StockNewsOnly) candidates = StockNewsFilter.Filter(candidates, Config.Watchlist);
        var visible = Config.HideRead ? candidates.Where(i => !readStore.IsRead(i.Url)).ToList() : candidates.ToList();
        if (visible.Count > 0) return visible;
        if (Config.StockNewsOnly && candidates.Count == 0)
            return new[] { Placeholder(Loc.T("pk.no.stories.matched.your")) };
        return new[] { Placeholder(Loc.T("ph.allRead")) };
    }

    // News of categories that are not the active one (shown in a bar or in one half of a split bar).
    private readonly Dictionary<string, List<NewsItem>> extraNews = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> extraNewsAt = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> extraNewsFailed = new(StringComparer.OrdinalIgnoreCase);
    private readonly RssNewsProvider extraNewsProvider = new();
    private bool refreshingExtra;

    private IReadOnlyList<NewsItem> CategoryNewsForDisplay(string id)
    {
        if (!extraNews.TryGetValue(id, out var items))
        {
            // First time: the stored copy of the last run, if any.
            var cached = DataCache.Load<List<NewsItem>>("news-" + id);
            items = cached?.Data ?? new List<NewsItem>();
            extraNews[id] = items;
            if (cached is not null && items.Count > 0) extraNewsAt[id] = cached.At;
        }
        var failed = extraNewsFailed.TryGetValue(id, out var f) ? f : new List<string>();
        DateTimeOffset? at = extraNewsAt.TryGetValue(id, out var t) ? t : null;
        return WithNotice(Notice(failed, at), FilterNews(items, items.Count > 0));
    }

    /// <summary>Fetches the categories of <see cref="ExtraNewsCategories"/>, one after the other, and keeps (and stores) the last good result of each.</summary>
    private IReadOnlyList<NewsItem> StocksForDisplay()
    {
        var failed = quotes.LastFailed.Select(QuoteService.ProviderName).Distinct().ToList();
        var storedAt = quotes.StaleSources.Count > 0 ? quotes.StaleSources.Values.Min() : (DateTimeOffset?)null;
        var notice = Notice(failed, storedAt);
        if (stockItems.Count > 0) return WithNotice(notice, stockItems);
        return new[] { notice ?? Placeholder(hasQuotes ? Loc.T("ph.quotesUnavailable") : Loc.T("ph.loadingQuotes")) };
    }

    private static NewsItem Placeholder(string text) => new(text, "", Loc.T("app.name"), DateTimeOffset.Now);

    /// <summary>Puts the current news / quotes into every bar, keeping the scroll positions.</summary>
    private void PushAll()
    {
        if (disposed) return;
        if (breakingActive && breakingItems.Count > 0)
        {
            // Urgent headlines take over the main bar until they are read or time out.
            Window.SetSplit(false);
            // Alerts that have no article (severe weather, price limits) cannot be "read": the normal news keeps scrolling after them,
            // so the bar never looks frozen. A click on an alert dismisses it.
            var urgent = breakingItems.Any(i => !string.IsNullOrEmpty(i.Url))
                ? (IEnumerable<NewsItem>)breakingItems
                : breakingItems.Concat(ItemsFor(Config.MainSlots().A));
            Window.SetItems(urgent, State.ScrollOffset);
        }
        else
        {
            PushTo(Window, State, Config.MainSlots());
        }
        if (SecondWindow is not null) PushTo(SecondWindow, State2, Config.SecondSlots());
        foreach (var (cfg, _) in WantedMonitors())
            if (monitorWindows.TryGetValue(cfg.Device, out var extra)) PushTo(extra.Window, extra.State, cfg.Slots());
        if (miniWeather is not null) PushTo(miniWeather, miniState, Config.MiniSlots());
    }

    private void PushTo(TickerWindow w, TickerState st, BarSlots slots)
    {
        if (slots.B is not { } second)
        {
            w.SetSplit(false);
            w.SetItems(ItemsFor(slots.A), st.ScrollOffset);
            return;
        }
        w.SetSplit(true);
        w.SetItems(ItemsFor(slots.A), st.ScrollOffset);
        w.SetItemsB(ItemsFor(second), st.ScrollOffsetB);
    }

    private IReadOnlyList<NewsItem> ItemsFor(BarSlot slot) => slot.Kind switch
    {
        SlotKind.Stocks => StocksForDisplay(),
        SlotKind.Weather => WeatherForDisplay(),
        SlotKind.Hazards => HazardsForDisplay(),
        SlotKind.Feed => PluginFeedForDisplay(slot.Category),
        _ => IsActiveCategory(slot.Category) ? NewsForDisplay() : CategoryNewsForDisplay(slot.Category!)
    };

    /// <summary>Forgets which articles were read; they show up in the strip again.</summary>
    public void ResetReadList()
    {
        readStore.Clear();
        if (allItems.Count > 0) PushAll();
    }

    // ---- breaking news and the live preview ----------------------------------------------

    private bool HasLive => Config.LiveFeeds.Any(f => f.Enabled && Breaking.ToPlayableUrl(f.Url) is not null);

    /// <summary>The settings button "forget my choices": the muted sources / topics and the "not breaking" answers are cleared.</summary>
    public void ForgetBreakingFeedback()
    {
        breakingFeedback.Clear();
        breakingFeedback.Save();
    }

    /// <summary>Finds new urgent headlines; the first time one is seen the intro plays and the bar turns red.</summary>
    private void DetectBreaking(List<NewsItem> items)
    {
        if (!Config.BreakingEnabled || (!Config.MainSlots().Has(SlotKind.News) && !breakingActive)) return;
        var engine = new BreakingEngine(new BreakingOptions(BreakingEngine.ParseSensitivity(Config.BreakingSensitivity), Config.BreakingKeywords,
            TimeSpan.FromMinutes(Config.BreakingMaxAgeMinutes), breakingFeedback));
        // Several signals, not one word: confirmation by other publishers, speed, freshness, source reliability, disaster data, negative filters (see BreakingEngine).
        // All headlines take part (the read ones too), so that a story keeps its confirmations; only unread ones are announced.
        var candidates = items.Where(i => !NewsKeywordFilter.IsMuted(i, Config.MutedNewsKeywords)).ToList();
        var urgent = engine.Urgent(candidates, hazardEvents, DateTimeOffset.Now)
            .Where(a => !readStore.IsRead(a.Item.Url))
            .Select(a => a.Item with { Breaking = true, Why = BreakingWhy.Detailed(a, BreakingEngine.ParseSensitivity(Config.BreakingSensitivity)) }).ToList();
        if (urgent.Count == 0) return;

        breakingVideoContext = urgent[0].Title;
        foreach (var item in urgent.Take(6))
            SuiteEventHub.Publish(new SuiteEvent(SuiteEventKind.Breaking, "crawler", item.Url,
                item.Title, item.PublishedAt, item.Url, item.Why));
        var isNew = urgent.Where(i => !readStore.IsRead("breaking:" + i.Url)).ToList();
        foreach (var i in isNew) readStore.MarkRead("breaking:" + i.Url); // remembered, so a restart does not replay the intro
        // Price and weather alerts have no link: they stay on the bar next to the urgent headlines.
        breakingItems = urgent.Concat(breakingItems.Where(i => string.IsNullOrEmpty(i.Url))).ToList();
        breakingUntil = DateTimeOffset.Now.AddMinutes(Config.BreakingDurationMinutes);
        if (!breakingActive && isNew.Count > 0) StartBreaking();
        else if (breakingActive)
        {
            if (isNew.Count > 0) PlayAlertSound();
            Window.SetLiveAvailable(HasLive);
        }
    }

    /// <summary>Shows an alert on the bar (like an urgent headline) for every price limit that was just passed.</summary>
    private void CheckPriceAlerts()
    {
        if (disposed || Config.PriceAlerts.Count == 0) return;
        var hits = PriceAlertCheck.Evaluate(Config.PriceAlerts, quotes.LastQuotes);
        if (hits.Count == 0) return;
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        foreach (var (alert, quote) in hits)
        {
            var label = string.IsNullOrWhiteSpace(alert.Label) ? quote.Symbol : alert.Label;
            var text = alert.Metric == "changePercent"
                ? $"{label}: {(quote.ChangePercent ?? 0m):+0.##;-0.##;0}% ({(alert.Above ? "≥" : "≤")}{alert.Threshold:0.##}%)"
                : Loc.T(alert.Above ? "alert.above" : "alert.below", label, alert.Threshold.ToString("0.00##", culture), quote.Price.ToString("0.00##", culture));
            breakingItems.Add(new NewsItem(text, "", Loc.T("pk.price.alert"), DateTimeOffset.Now,
                Summary: label, Kind: NewsItemKind.News, Trend: alert.Above ? 1 : -1, Breaking: true));
        }
        breakingUntil = DateTimeOffset.Now.AddMinutes(Config.BreakingDurationMinutes);
        if (!breakingActive) StartBreaking();
        PushAll();
    }

    // ---- weather and map of a city (click on the weather line) ---------------------------------

    private WeatherMapForm? weatherMap;

    private void ShowWeatherMap(NewsItem item, Rectangle barBounds)
    {
        if (disposed || !Config.WeatherMapOnClick || GreekCities.Find(item.Key) is not { } city) return;
        var reading = weatherReadings.FirstOrDefault(r => string.Equals(r.CityId, city.Id, StringComparison.OrdinalIgnoreCase));
        weatherMap?.Close();
        weatherMap = new WeatherMapForm(city, reading, weatherFailed, barBounds);
        var shown = weatherMap;
        shown.FormClosed += (_, _) => { if (ReferenceEquals(weatherMap, shown)) weatherMap = null; };
        shown.Show();
    }

    // ---- detail card of a disaster (click on the disaster line) ---------------------------------

    private HazardDetailForm? hazardDetail;

    private void ShowHazardDetail(NewsItem item, Rectangle barBounds)
    {
        if (disposed || hazardEvents.FirstOrDefault(e => e.Id == item.Key) is not { } hazard) return;
        hazardDetail?.Close();
        hazardDetail = new HazardDetailForm(hazard, barBounds);
        var shown = hazardDetail;
        shown.FormClosed += (_, _) => { if (ReferenceEquals(hazardDetail, shown)) hazardDetail = null; };
        shown.Show();
    }

    private void StartBreaking()
    {
        breakingActive = true;
        PlayAlertSound();
        Window.StartBreaking();
        Window.SetLiveAvailable(HasLive);
    }

    /// <summary>The short sound of an urgent alert, when the user wants it.</summary>
    private void PlayAlertSound()
    {
        if (!Config.BreakingSound) return;
        AlertSound.Play();
    }

    // ---- severe weather for the user's area ---------------------------------------------------

    private void ToggleLivePreview()
    {
        if (livePreview is { IsDisposed: false })
        {
            CloseLivePreview();
            return;
        }
        // The Live Video Hub is user-initiated, starts muted, and uses only configured channels.
        var choices = LiveVideoPolicy.Select(Config.LiveFeeds, breakingVideoContext);
        if (choices.Count == 0) return;
        livePreview = new LivePreviewForm(choices, Window.Bounds);
        livePreview.FormClosed += (_, _) => livePreview = null;
        livePreview.Show();
    }

    private void CloseLivePreview()
    {
        if (livePreview is { IsDisposed: false }) livePreview.Close();
        livePreview = null;
    }

    private bool quotesFailed;
    private bool quoteRetryPending;

    // ---- natural disasters --------------------------------------------------------------------------

    private async void ScheduleQuoteRetry()
    {
        if (quoteRetryPending || disposed) return;
        quoteRetryPending = true;
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(60), lifetimeToken);
            if (!disposed && BarsOn) _ = RefreshQuotesAsync();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            quoteRetryPending = false;
        }
    }

    // ---- portfolio window ---------------------------------------------------------------------------

    public QuoteService QuoteService => quotes;
    public EcbRates Ecb { get; } = new();
    private PortfolioForm? portfolio;
    private bool portfolioOpen;

    /// <summary>Raised on the UI thread after a run of the price sources (the portfolio window shows the new prices).</summary>
    public event Action? QuotesUpdated;

    public void SetPortfolioOpen(bool open)
    {
        portfolioOpen = open;
        if (!open) portfolio = null;
        else _ = RefreshQuotesAsync(force: true);
    }

    /// <summary>Asks the price sources again now (also when no bar shows prices).</summary>
    public void RefreshPortfolio() => _ = RefreshQuotesAsync(force: true);

    /// <summary>Opens the portfolio window (one at a time) and brings it to the front.</summary>
    public void ShowPortfolio()
    {
        if (disposed) return;
        if (portfolio is { IsDisposed: false })
        {
            portfolio.Activate();
            return;
        }
        portfolio = new PortfolioForm(this);
        portfolio.Show();
    }

    private AnalysisForm? analysis;

    /// <summary>Opens the market-analysis window (candles, back-test, fear &amp; greed, assistant); one at a time.</summary>
    public void ShowAnalysis(int page = 0)
    {
        if (disposed) return;
        if (analysis is { IsDisposed: false })
        {
            analysis.ShowPage(page);
            analysis.Activate();
            return;
        }
        analysis = new AnalysisForm(this);
        analysis.ShowPage(page);
        analysis.FormClosed += (_, _) => analysis = null;
        analysis.Show();
    }

    /// <summary>Test aid (--snapshot-analysis): opens the analysis window out of sight, shows one page, saves it as a PNG.</summary>
    public void SnapshotAnalysis(string pngPath, int page, int waitSeconds)
    {
        var form = new AnalysisForm(this) { StartPosition = FormStartPosition.Manual, Location = new Point(-4000, 0) };
        form.Show();
        form.ShowPage(page);
        var until = DateTime.UtcNow.AddSeconds(waitSeconds);
        while (DateTime.UtcNow < until) { Application.DoEvents(); Thread.Sleep(50); }
        using var bmp = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
        bmp.Save(pngPath);
        form.Close();
        form.Dispose();
    }

    /// <summary>Test aid (--snapshot-portfolio): opens the portfolio window out of sight, waits for the prices, saves it as a PNG.</summary>
    public void SnapshotPortfolio(string pngPath, int waitSeconds)
    {
        var form = new PortfolioForm(this) { StartPosition = FormStartPosition.Manual, Location = new Point(-4000, 0) };
        form.Show();
        var until = DateTime.UtcNow.AddSeconds(waitSeconds);
        while (DateTime.UtcNow < until)
        {
            Application.DoEvents();
            Thread.Sleep(50);
        }
        using var bmp = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
        bmp.Save(pngPath);
        form.Close();
    }

    private void OnPluginFeedsChanged()
    {
        if (disposed || Window is null || !Window.IsHandleCreated) return;   // a plugin can register a feed while the host is still being built
        try { Window.BeginInvoke(new Action(() => { if (!disposed) PushAll(); })); }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException) { }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Loc.Changed -= OnLanguageChanged;
        OpsisNewsCrawler.Plugin.Abstractions.PluginFeeds.Changed -= OnPluginFeedsChanged;
        if (pluginFeedCacheInvalidator is not null)
            OpsisNewsCrawler.Plugin.Abstractions.PluginFeeds.Changed -= pluginFeedCacheInvalidator;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaysChanged;
        Microsoft.Win32.SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        cts.Cancel();
        refreshScheduler.Dispose();
        refreshTimer.Dispose();
        performanceTimer.Dispose();
        updateTimer.Dispose();
        quoteTimer.Dispose();
        snoozeTimer.Dispose();
        hotkey?.Dispose();
        weatherTimer.Dispose();
        hazardTimer.Dispose();
        alertTimer.Dispose();
        miniWeather?.Dispose();
        breakingTimer.Dispose();
        CloseLivePreview();
        weatherMap?.Close();
        weatherMap = null;
        hazardDetail?.Close();
        hazardDetail = null;
        portfolio?.Close();
        portfolio = null;
        news.Dispose();
        SecondWindow?.Dispose();
        foreach (var extra in ExtraWindows) extra.Dispose();
        monitorWindows.Clear();
        Window.Dispose();
        cts.Dispose();
    }
}
