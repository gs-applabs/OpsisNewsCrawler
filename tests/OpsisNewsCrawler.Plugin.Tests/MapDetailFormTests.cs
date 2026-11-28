using OpsisNewsCrawler.Core;
using OpsisNewsCrawler.Windows;
using Xunit;

namespace OpsisNewsCrawler.Plugin.Tests;

public sealed class MapDetailFormTests
{
    [Fact]
    public void SharedMapWindowKeepsDistinctLinkPoliciesAndPresentation()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                const string official = "https://earthquake.usgs.gov/earthquakes/eventpage/test";
                var quake = new HazardEvent("usgs:test", HazardKind.Earthquake, "Crete", 35.2, 25.0,
                    DateTimeOffset.Now, official, 5.7, 5.5, 5.9, 2, new[] { "USGS" }, 12,
                    new Dictionary<string, string> { ["USGS"] = official });
                var anchor = new Rectangle(0, 650, 900, 40);
                using var hazard = new HazardDetailForm(quake, anchor);
                using var weather = new WeatherMapForm(new GreekCity("test", "Πόλη", "City", 35.2, 25.0), null, false, anchor);
                Assert.IsAssignableFrom<MapDetailForm>(hazard);
                Assert.IsAssignableFrom<MapDetailForm>(weather);
                Assert.True(hazard.AllowsExternalLink(official));
                Assert.False(hazard.AllowsExternalLink("https://example.org/unlisted"));
                Assert.False(hazard.AllowsExternalLink("http://earthquake.usgs.gov/earthquakes/eventpage/test"));
                Assert.False(weather.AllowsExternalLink(official));
                Assert.False(weather.AllowsExternalLink(null));
                Assert.False(hazard.ShowInTaskbar);
                Assert.False(weather.ShowInTaskbar);
                Assert.Equal(FormBorderStyle.None, hazard.FormBorderStyle);
                Assert.Equal(FormBorderStyle.None, weather.FormBorderStyle);
            }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "Map construction timed out");
        if (error is not null) throw new Exception("Map regression", error);
    }
}
