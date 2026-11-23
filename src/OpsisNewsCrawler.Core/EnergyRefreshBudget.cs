namespace OpsisNewsCrawler.Core;

/// <summary>
/// Power/connection budget layered on top of the existing hardware performance tier.
/// Never polls the network and never disables a user-requested refresh or safety alert.
/// </summary>
public sealed record EnergyRefreshBudget(double Multiplier, bool AllowBackgroundPrefetch, int MaximumFps)
{
    public static EnergyRefreshBudget For(HardwareSpecs? hardware, bool metered, PerformanceTier tier)
    {
        var saver = hardware?.BatterySaver == true;
        var battery = hardware?.OnBattery == true;
        var factor = (saver ? 1.5 : battery ? 1.25 : 1.0) * (metered ? 2.0 : 1.0);
        return new EnergyRefreshBudget(Math.Clamp(factor, 1.0, 3.0),
            AllowBackgroundPrefetch: !metered && !saver,
            MaximumFps: saver ? 24 : tier == PerformanceTier.Low || battery ? 30 : tier == PerformanceTier.Balanced ? 50 : 66);
    }

    public int RefreshMs(int baseSeconds)
    {
        var interval = (long)Math.Round(Math.Max(1, baseSeconds) * 1000d * Multiplier);
        return (int)Math.Clamp(interval, 1000, int.MaxValue - 1);
    }
}
