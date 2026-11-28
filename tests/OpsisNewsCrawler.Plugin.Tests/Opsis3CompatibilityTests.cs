using System.Text.Json;
using OpsisNewsCrawler.OpsisPlugin;
using Xunit;

namespace OpsisNewsCrawler.Plugin.Tests;

/// <summary>What the Opsis 3.0 host reads from a plugin folder (PluginCapabilityManifest): opsis-permissions.json, at most 4096 bytes, three booleans.</summary>
public sealed class Opsis3CompatibilityTests
{
    private static string PermissionsFile =>
        Path.Combine(Path.GetDirectoryName(typeof(OpsisNewsCrawlerPlugin).Assembly.Location)!, "opsis-permissions.json");

    [Fact]
    public void PluginFolderCarriesAValidCapabilityDeclaration()
    {
        Assert.True(File.Exists(PermissionsFile), "opsis-permissions.json must ship next to the plugin DLL");
        Assert.True(new FileInfo(PermissionsFile).Length <= 4096);
        using var doc = JsonDocument.Parse(File.ReadAllText(PermissionsFile));
        foreach (var name in new[] { "network", "audio", "localFiles" })
            Assert.Equal(JsonValueKind.True, doc.RootElement.GetProperty(name).ValueKind);   // all three are really used
        Assert.Equal(3, doc.RootElement.EnumerateObject().Count());
    }

    [Fact]
    public void PluginAbiIdentityMatchesWhatTheHostChecks()
    {
        // Opsis 2.8 and 3.0 skip a plugin DLL whose AssemblyVersion is not exactly 1.0.0.0.
        Assert.Equal(new Version(1, 0, 0, 0), typeof(OpsisNewsCrawlerPlugin).Assembly.GetName().Version);
    }
    [Fact]
    public void SafeModeHandshakeAndExplicitStopDoNotCreateTickerHost()
    {
        var plugin = new OpsisNewsCrawlerPlugin();
        var provider = new Func<double, double, Task<string>>((_, _) => Task.FromResult("{}"));
        plugin.SetOpsisP0Environment(true, true, provider);
        plugin.Start();
        plugin.Stop();
        plugin.SetOpsisP0Environment(false, false, provider);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var controller = typeof(OpsisNewsCrawlerPlugin).GetField("_controller", flags)!.GetValue(plugin)!;
        Assert.Null(controller.GetType().GetField("_host", flags)!.GetValue(controller));
    }

    [Fact]
    public void BlockedPluginViewsNeverConstructASecondRuntime()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var owner = new OpsisNewsCrawler.Core.InstanceGuard();
                Assert.True(owner.TryAcquire());
                var plugin = new OpsisNewsCrawlerPlugin();
                plugin.Start();
                using var page = plugin.GetNavigationItems().Single().CreatePage();
                var widgets = plugin.GetWidgets().Select(w => w.CreateContent()).ToArray();
                try
                {
                    plugin.SetMode("Economy");
                    plugin.SetPosition("Top");
                    var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                    var controller = typeof(OpsisNewsCrawlerPlugin).GetField("_controller", flags)!.GetValue(plugin)!;
                    Assert.Null(controller.GetType().GetField("_host", flags)!.GetValue(controller));
                    Assert.DoesNotContain(page.Controls.Cast<Control>(), c => c is OpsisNewsCrawler.Windows.SettingsPanel);
                    Assert.All(widgets, w => Assert.DoesNotContain(w.Controls.Cast<Control>(), c => c is OpsisNewsCrawler.Windows.CrawlerWidgetPanel));
                }
                finally
                {
                    foreach (var widget in widgets) widget.Dispose();
                    plugin.Stop();
                }
            }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(30000), "Ownership test timed out");
        if (error is not null) throw new Exception("Ownership regression", error);
    }
}
