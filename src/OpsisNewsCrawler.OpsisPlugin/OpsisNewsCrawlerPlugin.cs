using OpsisNewsCrawler.Core;
using OpsisNewsCrawler.Plugin.Abstractions;
using OpsisNewsCrawler.Windows;

namespace OpsisNewsCrawler.OpsisPlugin;

/// <summary>
/// Plugin entry point for Opsis - Desktop Customization. The host must create it and call it on its UI thread.
/// Hosted, the crawler never creates its own tray icon: its commands are offered through <see cref="GetTrayMenuItems"/>
/// and its settings are a tab of the host.
/// </summary>
public sealed class OpsisNewsCrawlerPlugin : IOpsisNewsCrawlerPlugin, IPluginProfileAware, IPluginWidgetProvider, IPluginHostSettingsAware
{
    public const string NavigationKey = "OpsisNewsCrawler";

    private readonly TickerController _controller = new();

    public OpsisNewsCrawlerPlugin()
    {
        // Diagnostics: when Opsis cannot show the plugin, crawler.log says which step failed.
        Log.Write($"Plugin {AboutTexts.Version} created inside {AppDomain.CurrentDomain.FriendlyName} (runtime {Environment.Version}).");
        _controller.SettingsRequested += OnSettingsRequested;
    }

    /// <inheritdoc />
    public event Action<string>? NavigationRequested;

    private void OnSettingsRequested()
    {
        // Preferred: the host shows the plugin's own tab. If nobody subscribed, open a settings window instead.
        if (NavigationRequested is { } navigate) navigate(NavigationKey);
        else _controller.ShowFallbackSettings();
    }

    public string Id => "giorstergiopoulos.opsisnewscrawler";
    public string Name => "Opsis News Crawler";
    // This is the plugin PRODUCT version used by Opsis to choose the newest installed copy.
    // DLL AssemblyVersion/FileVersion are intentionally pinned at 1.0.0.0 for ABI identity.
    public string Version => AboutTexts.Version;

    public void Start()
    {
        try { _controller.Start(); Log.Write("Plugin started."); }
        catch (Exception ex) { Log.Write($"Plugin Start failed: {ex}"); throw; }
    }
    public void Stop() => _controller.Stop();

    /// <summary>Tray "Close": the bars go away and nothing is fetched; "Open" brings them back. The plugin itself stays loaded.</summary>
    private void CloseCrawler()
    {
        _controller.Stop();
        Log.Write("Crawler closed from the tray menu.");
    }

    private void OpenCrawler()
    {
        _controller.Start();
        Log.Write("Crawler opened from the tray menu.");
    }
    public void Pause() => _controller.Pause();
    public void Resume() => _controller.Resume();
    public void Refresh() => _controller.Refresh();
    public void SetMode(string mode) => _controller.SetMode(TickerParsing.ParseMode(mode));
    public void SetPosition(string position) => _controller.SetPosition(TickerParsing.ParsePosition(position));

    public void SetTheme(bool dark, int background, int surface, int border, int text, int textMuted, int accent, double opacity)
    {
        _controller.Context.AcceptTheme(dark);
        Theme.SetColors(dark, Color.FromArgb(background), Color.FromArgb(surface), Color.FromArgb(border),
            Color.FromArgb(text), Color.FromArgb(textMuted), Color.FromArgb(accent), Theme.Opacity);
    }

    public void SetHardwareProfile(string tier) => _controller.SetHostTier(tier);

    /// <summary>Optional Opsis host P0 handshake. No plugin contract ABI change:
    /// the trusted host discovers this method by name and supplies its own
    /// cached WeatherHub callback for the cities configured in Crawler.</summary>
    public void SetOpsisP0Environment(bool safeMode, bool shellHold, Func<double, double, Task<string>> weatherProvider)
    {
        ArgumentNullException.ThrowIfNull(weatherProvider);
        _controller.Context.AcceptSafeMode(safeMode);
        _controller.Context.AcceptShellHold(shellHold);
        _controller.ConfigureP0Environment(safeMode, shellHold, weatherProvider);
    }

    /// <summary>Optional Opsis 2.8 host-owned settings. No UI creation,
    /// changes to process-global Opsis.UI or file writes.</summary>
    public void SetHostSettings(PluginHostSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _controller.Context.AcceptTheme(settings.Dark);
        _controller.Context.AcceptLanguage(settings.Language);
        _controller.Context.AcceptTier(settings.HardwareTier);
        _controller.Context.AcceptUiScale(settings.UiScalePercent);
        _controller.LanguageFromHost = true;
        Loc.SetLanguage(_controller.Context.Snapshot.Language);
        _controller.SetHostTier(settings.HardwareTier);
    }

    public void SetLanguage(string code)
    {
        _controller.LanguageFromHost = true;
        _controller.Context.AcceptLanguage(code);
        Loc.SetLanguage(code);
    }

    public PluginAboutInfo GetAboutInfo() => new(
        Name,
        Version,
        "Μπάρα ειδήσεων και τιμών για την επιφάνεια εργασίας: RSS ανά κατηγορία, τιμές Euronext Athens, μετάλλων και πετρελαίου, δύο μπάρες.",
        "News and price bar for the desktop: RSS per category, Euronext Athens, metals and oil prices, two bars.",
        "Opsis Team",
        "MIT",
        new[]
        {
            new PluginAboutSection("Βοήθεια", "Help", AboutTexts.Help),
            new PluginAboutSection("Οδηγίες χρήσης", "Instructions", AboutTexts.Instructions),
            new PluginAboutSection("Ιστορικό εκδόσεων", "Version history", AboutTexts.History),
            new PluginAboutSection("Άδεια χρήσης", "License", AboutTexts.License),
        });

    public IReadOnlyList<PluginMenuItem> GetTrayMenuItems() => BuildTrayMenu(
        OnSettingsRequested, Refresh,
        () => { if (_controller.IsPaused) Resume(); else Pause(); }, CloseCrawler, OpenCrawler);

    /// <summary>
    /// The commands the crawler adds to Opsis' tray menu. The welcome guide and the Opsis family window are NOT here: inside Opsis the host offers them itself,
    /// once (the standalone program has them in its own tray menu).
    /// </summary>
    public static IReadOnlyList<PluginMenuItem> BuildTrayMenu(Action settings, Action refresh, Action pauseResume, Action close, Action open) => new[]
    {
        new PluginMenuItem("news.separator", "", "", () => { }, IsSeparator: true),
        new PluginMenuItem("news.settings", "News Crawler — Ρυθμίσεις", "News Crawler — Settings", settings),
        new PluginMenuItem("news.refresh", "News Crawler — Ανανέωση", "News Crawler — Refresh", refresh),
        new PluginMenuItem("news.pause", "News Crawler — Παύση / Συνέχεια", "News Crawler — Pause / Resume", pauseResume),
        new PluginMenuItem("news.close", "News Crawler — Κλείσιμο", "News Crawler — Close", close),
        new PluginMenuItem("news.open", "News Crawler — Άνοιγμα", "News Crawler — Open", open),
    };

    /// <summary>Host desktop widgets use the common Opsis palette and only in-memory data.</summary>
    public IReadOnlyList<PluginWidget> GetWidgets() => new[]
    {
        new PluginWidget("crawler-news", "Ειδήσεις", "News", () => new CrawlerWidgetPanel(_controller.Host, "news")),
        new PluginWidget("crawler-markets", "Αγορές", "Markets", () => new CrawlerWidgetPanel(_controller.Host, "markets")),
        new PluginWidget("crawler-weather", "Καιρός", "Weather", () => new CrawlerWidgetPanel(_controller.Host, "weather")),
        new PluginWidget("crawler-breaking", "Έκτακτες", "Breaking", () => new CrawlerWidgetPanel(_controller.Host, "breaking")),
        new PluginWidget("crawler-history", "Σαν σήμερα", "On this day", () => new CrawlerWidgetPanel(_controller.Host, "history"))
    };

    public IReadOnlyList<PluginNavigationItem> GetNavigationItems() =>
        new[]
        {
            new PluginNavigationItem(
                NavigationKey,
                "News Crawler",
                "News Crawler",
                () =>
                {
                    try { return new SettingsPanel(_controller.Host, showCloseButton: false, showAboutTabs: false) { HostThemeLocked = true }; }
                    catch (Exception ex) { Log.Write($"Plugin settings page failed: {ex}"); throw; }
                },
                90)
        };
}

/// <summary>Bridges the plugin API to the real ticker (the same TickerHost the standalone app uses).</summary>
internal sealed class TickerController
{
    private TickerHost? _host;
    private SettingsForm? _fallbackSettings;

    public HostContextState Context { get; } = new(hosted: true);
    public bool IsPaused => _host?.Paused ?? false;

    private bool hostSafeMode;
    private bool shellHold;
    private Func<double, double, Task<string>>? weatherProvider;

    public void ConfigureP0Environment(bool safeMode, bool hold, Func<double, double, Task<string>> provider)
    {
        hostSafeMode = safeMode;
        shellHold = hold;
        weatherProvider = provider;
        if (_host is null) return; // host sets these before Start(), no eager UI
        _host.HostSafeMode = safeMode;
        _host.HostWeatherProvider = provider;
        _host.SetHostShellHold(hold);
        if (safeMode) _host.Stop();
    }

    public void SetHostTier(string? tier)
    {
        Context.AcceptTier(tier);
        _host?.SetHostTier(Context.Snapshot.PerformanceTier);
    }

    /// <summary>The host (Opsis) told us its language: the crawler's own language setting is then ignored.</summary>
    public bool LanguageFromHost { get; set; }

    public event Action? SettingsRequested;

    public TickerHost Host
    {
        get
        {
            if (_host is null)
            {
                _host = new TickerHost { HostedByOpsis = true, LanguageFollowsHost = LanguageFromHost };
                if (LanguageFromHost) Loc.SetLanguage(Context.Snapshot.Language);
                _host.SetHostTier(Context.Snapshot.PerformanceTier);
                _host.HostSafeMode = hostSafeMode;
                _host.SetHostShellHold(shellHold);
                _host.HostWeatherProvider = weatherProvider;
                if (!_host.Config.ThemeIndependentMigrated)
                {
                    _host.Config.ThemeIndependentMigrated = true;
                    _host.Config.ThemeMode = "host";
                    try { _host.Config.Save(); } catch (Exception) { }
                }
                _host.ApplyConfig(); // HostedByOpsis changes the default for reserving screen space
                _host.SettingsRequested += () => SettingsRequested?.Invoke();
            }
            return _host;
        }
    }

    public void ShowFallbackSettings()
    {
        _fallbackSettings ??= new SettingsForm(Host);
        _fallbackSettings.ShowSettings();
    }

    private readonly InstanceGuard _guard = new();

    /// <summary>True when the stand-alone program is running: the plugin then starts no second bar (one crawler, one settings file).</summary>
    public bool StandaloneRunning { get; private set; }

    public void Start()
    {
        if (hostSafeMode)
        {
            Log.Write("Crawler disabled by Opsis Safe Mode.");
            return;
        }
        if (!_guard.TryAcquire())
        {
            StandaloneRunning = true;
            Log.Write("Plugin: the stand-alone Opsis News Crawler is running; the plugin does not start a second bar.");
            return;
        }
        StandaloneRunning = false;
        try { Host.Start(); }
        catch { _guard.Release(); throw; }
    }

    public void Stop()
    {
        _host?.Stop();
        _guard.Release();
    }
    public void Pause() => _host?.Pause();
    public void Resume() => _host?.Resume();
    public void Refresh() => _host?.Refresh();
    public void SetMode(TickerMode mode) => Host.SetMode(mode);
    public void SetPosition(TickerPosition position) => Host.SetPosition(position);
}
