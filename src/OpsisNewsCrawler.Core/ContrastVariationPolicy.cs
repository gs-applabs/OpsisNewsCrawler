namespace OpsisNewsCrawler.Core;

/// <summary>
/// Mixed wallpaper can contain both very dark and very bright areas. The median alone may
/// choose a text colour that is illegible elsewhere; enable text outlining/shadow in that case.
/// </summary>
public static class ContrastVariationPolicy
{
    public static bool NeedsShadow(double percentile10, double percentile90, bool useDarkText)
    {
        if (!double.IsFinite(percentile10) || !double.IsFinite(percentile90)) return true;
        var low = Math.Clamp(Math.Min(percentile10, percentile90), 0, 1);
        var high = Math.Clamp(Math.Max(percentile10, percentile90), 0, 1);
        return Math.Min(ContrastChooser.Contrast(low, useDarkText),
                        ContrastChooser.Contrast(high, useDarkText)) < 4.5;
    }
}
