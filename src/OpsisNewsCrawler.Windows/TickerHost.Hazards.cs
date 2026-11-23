using OpsisNewsCrawler.Core;
using System.Diagnostics;

namespace OpsisNewsCrawler.Windows;

/// <summary>Weather and hazard services extracted from the host; shares lifecycle and theme fields.</summary>
public sealed partial class TickerHost
{
    private readonly HazardService hazardService = new();
    private readonly System.Windows.Forms.Timer hazardTimer = new() { Interval = 15 * 60 * 1000 };
    private IReadOnlyList<HazardEvent> hazardEvents = Array.Empty<HazardEvent>();
    private IReadOnlyList<string> hazardFailed = Array.Empty<string>();
    private int hazardSkipped;
    private bool refreshingHazards;
    private bool hasHazards;
    private DateTimeOffset? hazardDataAt;

    /// <summary>Disasters are fetched when a bar shows them, or when the animated icon should show the highest alert.</summary>
    private bool NeedsHazards => Needs(SlotKind.Hazards) || Config.HazardInIcon;

    private HazardFilter HazardFilterNow() => new(Config.HazardRegion, Config.HazardMinMagnitude, Config.HazardMaxAgeHours,
        Config.HazardKinds.Count == 0 ? Enum.GetValues<HazardKind>() : Config.HazardKinds.Select(Enum.Parse<HazardKind>).ToList());

    private IReadOnlyList<HazardEvent> VisibleHazards() => HazardFilterNow().Apply(hazardEvents, DateTimeOffset.Now);

    private IReadOnlyList<NewsItem> HazardsForDisplay()
    {
        if (!hasHazards) return new[] { Placeholder(Loc.T("ph.loadingWeather")) };
        var now = DateTimeOffset.Now;
        var visible = VisibleHazards();
        var items = new List<NewsItem>();
        if (Config.HazardRegion == "world")
        {
            // The whole world: one block per continent, each starting with the picture and the name of the continent.
            foreach (var (continent, events) in HazardWorld.Group(visible.Take(60)))
            {
                items.Add(HazardText.ContinentItem(continent));
                items.AddRange(events.Take(8).Select(e => HazardText.ToItem(e, now)));
            }
        }
        else items.AddRange(visible.Take(15).Select(e => HazardText.ToItem(e, now)));
        var notices = new List<NewsItem>();
        if (hazardFailed.Count > 0) notices.Add(Placeholder(Loc.T("hazard.failed", string.Join(", ", hazardFailed))));
        // A partial list (some items of a feed could not be read) is told apart from a list that is empty because nothing is happening.
        if (hazardSkipped > 0) notices.Add(Placeholder(Loc.T("hazard.partial", hazardSkipped)));
        if (items.Count == 0) return new[] { notices.FirstOrDefault() ?? Placeholder(Loc.T("hazard.none")) };
        return notices.Count == 0 ? items : notices.Concat(items).ToList();
    }

    private async Task RefreshHazardsAsync()
    {
        if (disposed || !NeedsHazards || refreshingHazards) return;
        refreshingHazards = true;
        try
        {
            var events = await hazardService.FetchAsync((HazardSources)Config.HazardSources, lifetimeToken);
            var failed = hazardService.LastFailed;
            var skipped = hazardService.LastSkipped;
            void Apply()
            {
                if (disposed) return;
                hasHazards = true;
                hazardFailed = failed;
                hazardSkipped = skipped;
                if (events.Count > 0 || failed.Count == 0) { hazardEvents = events; hazardDataAt = DateTimeOffset.Now; }
                UpdateLiveIcon();
                PushAll();
            }
            if (!Window.IsHandleCreated) return;
            if (Window.InvokeRequired) Window.BeginInvoke(new Action(Apply)); else Apply();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Log.Write($"disasters failed: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            refreshingHazards = false;
        }
    }

    /// <summary>
    /// What the two live symbols of the animated icon show: the weather of the first city of the weather list and the highest level of the
    /// alerts that are active (disasters in the chosen region, a severe-weather warning). The tray tooltip gets the same in words.
    /// </summary>
    private void UpdateLiveIcon()
    {
        if (disposed) return;
        var icon = LiveIconState.WeatherIcon;
        var tip = new List<string>();
        var first = Config.WeatherCities.FirstOrDefault();
        var reading = weatherReadings.FirstOrDefault(r => string.Equals(r.CityId, first, StringComparison.OrdinalIgnoreCase)) ?? weatherReadings.FirstOrDefault();
        if (reading is not null)
        {
            icon = (int)WeatherInfo.KindOf(reading.Code) + (reading.IsDay ? 0 : 10);
            var city = GreekCities.Find(reading.CityId);
            tip.Add((city is null ? "" : (Loc.IsGreek ? city.NameEl : city.NameEn) + " ") + reading.TemperatureC.ToString("0.0", System.Globalization.CultureInfo.GetCultureInfo("el-GR")) + " °C");
        }
        var level = 0;
        if (Config.HazardInIcon)
        {
            var visible = VisibleHazards();
            if (visible.Count > 0)
            {
                var worst = visible.OrderByDescending(e => e.Level).ThenByDescending(e => e.Time).First();
                level = worst.Level;
                tip.Add(HazardText.Tip(worst));
            }
        }
        if (weatherAlertKeys.Count > 0) { level = Math.Max(level, 2); tip.Add(Loc.T("pk.severe.weather")); }
        LiveIconState.Set(icon, level, string.Join(" · ", tip));
    }

    // ---- weather ---------------------------------------------------------------------------------
}
