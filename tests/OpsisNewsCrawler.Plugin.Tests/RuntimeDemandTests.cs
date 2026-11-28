using OpsisNewsCrawler.Core;
using OpsisNewsCrawler.Windows;
using Xunit;

namespace OpsisNewsCrawler.Plugin.Tests;

[Collection("p0-gdi-soak")]
public sealed class RuntimeDemandTests
{
    [Fact]
    public void DisabledBarsAndUpdatesDoNotStartRefreshServicesOrLoadMarketHistory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "crawler-demand-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var path = Path.Combine(dir, "config.json");
                new AppConfig
                {
                    BarsEnabled = false, CheckForUpdates = false, HistoryFromYahoo = false,
                    WeatherInIcon = false, HazardInIcon = false, WeatherAlertsEnabled = false
                }.Save(path);
                using var host = new TickerHost(path);
                Assert.False(host.MarketStoreInitialized);
                host.Start();
                Assert.Empty(host.ActiveRefreshServices);
                Assert.False(host.HasActiveRefreshTimers);
                Assert.True(!host.AutomaticUpdatesActive, "Automatic updates must stay disabled after migration");
                Assert.True(!host.MarketStoreInitialized, "Disabled market feature must not load history");
                host.Config.CheckForUpdates = true;
                host.ApplyConfig();
                Assert.True(host.AutomaticUpdatesActive);
                host.Config.CheckForUpdates = false;
                host.ApplyConfig();
                Assert.False(host.AutomaticUpdatesActive);
                Assert.Empty(host.ActiveRefreshServices);
                // No live network is needed for this isolated demand check.
                foreach (var category in host.Config.Categories)
                    foreach (var feed in category.Feeds) feed.Enabled = false;
                host.Config.BreakingEnabled = true;
                host.SetWidgetVisible("breaking", true);
                Assert.Equal(new[] { "news" }, host.ActiveRefreshServices);
                Assert.False(host.Window.Visible);
                Assert.False(host.MarketStoreInitialized);
                host.SetWidgetVisible("breaking", true);
                host.SetWidgetVisible("breaking", false);
                Assert.Equal(new[] { "news" }, host.ActiveRefreshServices);
                host.Config.BreakingEnabled = false;
                host.ApplyConfig();
                Assert.Empty(host.ActiveRefreshServices);
                Assert.Empty(host.WidgetSnapshot("breaking"));
                host.Config.BreakingEnabled = true;
                host.ApplyConfig();
                Assert.Equal(new[] { "news" }, host.ActiveRefreshServices);
                host.SetWidgetVisible("breaking", false);
                Assert.Empty(host.ActiveRefreshServices);
                host.Stop();
                Assert.False(host.HasActiveRefreshTimers);
                Assert.False(host.MarketStoreInitialized);
            }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        try
        {
            Assert.True(thread.Join(60000), "Demand test timed out");
            if (error is not null) throw new Exception("Runtime demand regression", error);
        }
        finally { Directory.Delete(dir, true); }
    }
}
