using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public sealed class P1EnergyBudgetTests
{
    [Fact]
    public void UnmeteredDesktopUsesUnscaledTimers()
    {
        var budget = EnergyRefreshBudget.For(new HardwareSpecs(12,32,true,false,false),false,PerformanceTier.High);
        Assert.Equal(1.0,budget.Multiplier);
        Assert.Equal(60000,budget.RefreshMs(60));
        Assert.True(budget.AllowBackgroundPrefetch);
        Assert.Equal(66,budget.MaximumFps);
    }

    [Fact]
    public void BatteryAndMeteredScaleRefreshWithoutDisablingAlerts()
    {
        var budget = EnergyRefreshBudget.For(new HardwareSpecs(4,8,true,true,true),true,PerformanceTier.Low);
        Assert.Equal(3,budget.Multiplier);
        Assert.Equal(180000,budget.RefreshMs(60));
        Assert.False(budget.AllowBackgroundPrefetch);
        Assert.Equal(24,budget.MaximumFps);
    }
}
