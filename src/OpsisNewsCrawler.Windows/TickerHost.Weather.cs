using OpsisNewsCrawler.Core;
using OpsisNewsCrawler.Plugin.Abstractions;
using System.Diagnostics;

namespace OpsisNewsCrawler.Windows;

/// <summary>Weather and hazard services extracted from the host; shares lifecycle and theme fields.</summary>
public sealed partial class TickerHost
{
    private readonly WeatherAlertService alertService = new();
    private readonly System.Windows.Forms.Timer alertTimer = new() { Interval = 30 * 60 * 1000 };
    private HashSet<string> weatherAlertKeys = new();
    private bool checkingAlerts;

    /// <summary>
    /// Every 30 minutes: warnings of severe weather in the next 12 hours for the chosen city. A warning shows once like an urgent headline
    /// (with the sound) and again only after the condition has passed. These are computed from the forecast, not the official bulletin.
    /// </summary>
    private async Task CheckWeatherAlertsAsync()
    {
        if (disposed || checkingAlerts || !Config.WeatherAlertsEnabled || !Config.BreakingEnabled) return;
        checkingAlerts = true;
        try
        {
            var city = Config.ResolveAlertCity();
            var found = await alertService.FetchAsync(city, lifetimeToken);
            if (found is null || disposed) return;
            void Apply()
            {
                if (disposed) return;
                var keys = found.Select(a => city.Id + ":" + a.Kind).ToHashSet();
                var fresh = found.Where(a => !weatherAlertKeys.Contains(city.Id + ":" + a.Kind)).ToList();
                weatherAlertKeys = keys;
                if (fresh.Count == 0) return;
                AddWeatherAlertItems(city, fresh);
            }
            if (Window.IsHandleCreated && Window.InvokeRequired) Window.BeginInvoke(new Action(Apply)); else Apply();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Log.Write($"weather alerts failed: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            checkingAlerts = false;
        }
    }

    private const string WeatherWarningsUrl = "https://www.meteo.gr/warnings.cfm";

    private void AddWeatherAlertItems(GreekCity city, IReadOnlyList<WeatherAlert> alerts)
    {
        var name = Loc.IsGreek ? city.NameEl : city.NameEn;
        foreach (var a in alerts)
        {
            var text = $"⚠ {name}: {Loc.Pick(a.TextEl, a.TextEn)}";
            // A link makes the alert open something when clicked (the Hellenic weather warnings page) and end the alert.
            breakingItems.Add(new NewsItem(text, WeatherWarningsUrl, Loc.T("pk.severe.weather"), DateTimeOffset.Now,
                Summary: Loc.T("pk.automatic.warning.from.the"),
                Kind: NewsItemKind.News, Trend: -1, Breaking: true));
        }
        breakingUntil = DateTimeOffset.Now.AddMinutes(Config.BreakingDurationMinutes);
        if (!breakingActive) StartBreaking(); else PlayAlertSound();
        PushAll();
    }

    /// <summary>For testing: shows a sample severe-weather warning for the chosen city.</summary>
    public void SimulateWeatherAlert() =>
        AddWeatherAlertItems(Config.ResolveAlertCity(), new[] { new WeatherAlert("test", "Δοκιμαστική ειδοποίηση: ισχυροί άνεμοι, ριπές έως 80 km/h", "Test warning: strong winds, gusts up to 80 km/h", DateTimeOffset.Now) });

    private void EndBreaking()
    {
        if (!breakingActive) return;
        breakingActive = false;
        breakingItems = new();
        CloseLivePreview();
        Window.EndBreaking();
        PushAll();
    }

    /// <summary>For testing: behaves as if the given headline had just been published as urgent.</summary>
    public void SimulateBreaking(string title)
    {
        var item = new NewsItem(title, "https://example.com/breaking-test", "Test", DateTimeOffset.Now, "Breaking news test", null, NewsItemKind.News, 0, true);
        breakingItems = new List<NewsItem> { item };
        breakingUntil = DateTimeOffset.Now.AddMinutes(Config.BreakingDurationMinutes);
        if (!breakingActive) StartBreaking();
        PushAll();
    }

    private readonly WeatherService weather = new();

    // Bound only by Opsis 2.8's trusted PluginHost. It uses the *host-owned*
    // WeatherHub/cache for selected cities and never hands the user's location
    // to an external process or writes an extra cache file for successful results.
    public Func<double, double, Task<string>>? HostWeatherProvider { get; set; }

    private async Task<IReadOnlyList<WeatherReading>> TryHostWeatherAsync(IReadOnlyList<string> ids, CancellationToken ct)
    {
        var provider = HostWeatherProvider;
        if (!HostedByOpsis || provider is null) return Array.Empty<WeatherReading>();
        var readings = new List<WeatherReading>(ids.Count);
        foreach (var cityId in ids.Take(12))
        {
            ct.ThrowIfCancellationRequested();
            var city = GreekCities.Find(cityId);
            if (city is null) continue;
            try
            {
                var json = await provider(city.Lat, city.Lon).WaitAsync(TimeSpan.FromSeconds(12), ct);
                var parsed = HostWeatherPayload.Parse(city.Id, json, DateTimeOffset.UtcNow);
                if (parsed is not null) readings.Add(parsed);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) { Log.Write("Opsis shared weather unavailable: " + ex.GetType().Name); }
        }
        return readings;
    }
    private readonly System.Windows.Forms.Timer weatherTimer = new() { Interval = 15 * 60 * 1000 };
    private IReadOnlyList<WeatherReading> weatherReadings = Array.Empty<WeatherReading>();
    private bool refreshingWeather;
    private bool hasWeather;
    private bool weatherFailed;
    private DateTimeOffset? weatherDataAt;
    private TickerWindow? miniWeather;
    private readonly TickerState miniState = new();

    private bool NeedsWeatherForBars => Needs(SlotKind.Weather);

    /// <summary>Weather is fetched when a bar shows it, and for the first city when the animated icon shows it.</summary>
    private bool NeedsWeather => NeedsWeatherForBars || Config.WeatherInIcon;

    private IReadOnlyList<NewsItem> WeatherForDisplay()
    {
        if (Config.WeatherCities.Count == 0) return new[] { Placeholder(Loc.T("ph.noCities")) };
        var items = WeatherService.ToItems(Config.WeatherCities, weatherReadings, Loc.IsGreek, Config.WeatherFields);
        var notice = weatherFailed ? Notice(new[] { "Open-Meteo" }, weatherDataAt) : null;
        if (items.Count > 0) return WithNotice(notice, items);
        return new[] { notice ?? Placeholder(hasWeather ? Loc.T("ph.weatherUnavailable") : Loc.T("ph.loadingWeather")) };
    }

    private async Task RefreshWeatherAsync()
    {
        if (disposed || !started || !BarsOn || !NeedsWeather || refreshingWeather) return;
        var epoch = startEpoch;
        var token = SessionToken;
        refreshingWeather = true;
        try
        {
            var ids = Config.WeatherCities.Distinct().ToList();
            if (!NeedsWeatherForBars) ids = ids.Take(1).ToList();   // only the icon wants it
            var shared = await TryHostWeatherAsync(ids, token);
            var fromHost = shared.Count > 0;
            var readings = fromHost ? shared : await weather.FetchAsync(ids, token);
            var failed = !fromHost && weather.LastFailed;
            void Apply()
            {
                if (disposed || !started || epoch != startEpoch || !BarsOn) return;
                hasWeather = true;
                weatherFailed = failed;
                if (readings.Count > 0)
                {
                    weatherReadings = readings; // a failed request returns the previous readings: they stay, with a message
                    if (!failed)
                    {
                        weatherDataAt = DateTimeOffset.Now;
                        if (!fromHost) DataCache.Save("weather", readings.ToList()); // Opsis already caches its own weather
                    }
                }
                foreach (var item in WeatherForDisplay().Where(x => x.Kind == NewsItemKind.Weather).Take(15))
                    PublishSuiteEvent(new SuiteEvent(SuiteEventKind.Weather, "crawler", item.Key ?? item.Title,
                        item.Title, item.PublishedAt, item.Url, item.Summary));
                PushAll();
                UpdateMiniWeather();
                UpdateLiveIcon();
            }
            if (!Window.IsHandleCreated) return;
            if (Window.InvokeRequired) Window.BeginInvoke(new Action(Apply)); else Apply();
            if (started && epoch == startEpoch && (readings.Count == 0 || failed)) _ = RetryWeatherLater();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Log.Write($"weather failed: {ex.GetType().Name}: {ex.Message}");
            if (started && epoch == startEpoch)
            {
                hasWeather = true;
                weatherFailed = true;
                _ = RetryWeatherLater();
            }
        }
        finally
        {
            refreshingWeather = false;
        }
    }

    private async Task RetryWeatherLater()
    {
        var epoch = startEpoch;
        var token = SessionToken;
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(60), token);
            if (!disposed && started && epoch == startEpoch && BarsOn) _ = RefreshWeatherAsync();
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>Where the small weather bar is: the saved place, or the middle of the primary desktop.</summary>

    public Rectangle GetMiniWeatherBounds()
    {
        var wa = (Screen.PrimaryScreen ?? Screen.AllScreens[0]).WorkingArea;
        var w = Math.Min(Config.MiniWeatherWidth, wa.Width);
        var h = Config.MiniWeatherHeight;
        var onScreen = Config.MiniWeatherX != int.MinValue && Config.MiniWeatherY != int.MinValue &&
                       Screen.AllScreens.Any(sc => sc.WorkingArea.IntersectsWith(new Rectangle(Config.MiniWeatherX, Config.MiniWeatherY, w, h)));
        return onScreen
            ? new Rectangle(Config.MiniWeatherX, Config.MiniWeatherY, w, h)
            : new Rectangle(wa.Left + (wa.Width - w) / 2, wa.Top + (wa.Height - h) / 2, w, h);
    }

    /// <summary>Puts the small weather bar back in the middle of the desktop.</summary>
    public void CenterMiniWeather()
    {
        Config.MiniWeatherX = int.MinValue;
        Config.MiniWeatherY = int.MinValue;
        SaveConfig();
        UpdateMiniWeather();
        MiniWeatherBoundsChanged?.Invoke();
    }

    /// <summary>Raised after the small weather bar was moved or resized with the mouse.</summary>
    public event Action? MiniWeatherBoundsChanged;

    /// <summary>Lets the user move the small weather bar with the mouse (and change its width by the ends).</summary>
    public void SetMiniWeatherEditMode(bool on) => miniWeather?.SetEditMode(on);

    public bool IsMiniWeatherEditMode => miniWeather?.IsEditMode == true;

    /// <summary>Creates, updates or removes the small weather bar of the desktop according to the settings. It is an ordinary bar (same colour, transparency, material, fading ends), only smaller, centred and on the desktop.</summary>
    private void UpdateMiniWeather()
    {
        if (disposed) return;
        if (!(BarsOn && MiniWanted))
        {
            if (miniWeather is not null)
            {
                miniWeather.StopTicker();
                miniWeather.Dispose();
                miniWeather = null;
            }
            return;
        }
        var created = miniWeather is null;
        if (created)
        {
            miniWeather = new TickerWindow(miniState, hostWindowDetector);
            WireWindow(miniWeather);
            miniWeather.PlacementEdited += b =>
            {
                Config.MiniWeatherX = b.X;
                Config.MiniWeatherY = b.Y;
                Config.MiniWeatherWidth = b.Width;
                SaveConfig();
                MiniWeatherBoundsChanged?.Invoke();
            };
        }
        var w = miniWeather!;
        ConfigureWindow(w, TickerPosition.Custom, main: false);
        w.FollowTaskbar = false;
        w.ReserveSpace = false;
        w.SetCenterWhenStatic(true);
        w.SetBackdropStyle(Config.BarBackdrop);
        w.SetBarColor(Config.BarColorMode, Config.BarCustomColor);
        w.SetBarHeight(Config.MiniWeatherHeight);
        // Only the user's choice decides: unchecked = on the desktop layer, behind every window (also when it stands in for a hidden taskbar).
        w.SetDesktopLayer(!Config.MiniWeatherOnTop);
        w.MatchDeskStrip = true;      // a chosen colour (BarColorMode) still wins: ApplyLook handles the tint first
        w.SetShape(Config.DesktopBarShape);
        w.ApplyLook();
        w.SetCustomBounds(GetMiniWeatherBounds());
        w.SetPosition(TickerPosition.Custom);
        PushTo(w, miniState, Config.MiniSlots());
        if (created) w.StartTicker();
    }

    /// <summary>After a failed run try again in a minute instead of waiting for the normal interval.</summary>
}
