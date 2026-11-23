using OpsisNewsCrawler.OpsisPlugin;
using OpsisNewsCrawler.Plugin.Abstractions;
using Xunit;

namespace OpsisNewsCrawler.Plugin.Tests;

public sealed class P1WidgetContractTests
{
    [Fact]
    public void WidgetsUseAdditiveHostCapabilityAndHaveUniqueKeys()
    {
        var plugin = new OpsisNewsCrawlerPlugin();
        Assert.IsAssignableFrom<IPluginWidgetProvider>(plugin);
        var widgets = ((IPluginWidgetProvider)plugin).GetWidgets();
        Assert.Equal(5, widgets.Count);
        Assert.Equal(widgets.Count, widgets.Select(w => w.Key).Distinct().Count());
        Assert.All(widgets, w => Assert.Null(w.ReplacesBuiltIn));
        Assert.Contains(widgets, w => w.Key == "crawler-history");
    }
}
