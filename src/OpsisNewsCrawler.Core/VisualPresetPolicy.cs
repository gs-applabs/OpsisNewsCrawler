namespace OpsisNewsCrawler.Core;

/// <summary>Named appearance presets built from the same Opsis theme tokens; no new theme implementation.</summary>
public sealed record VisualPreset(string Key, string ThemeName, string Backdrop,
    int Opacity, bool EdgeFade, bool Shadow, bool AutoText);

public static class VisualPresetPolicy
{
    public static IReadOnlyList<VisualPreset> All { get; } = new[]
    {
        new VisualPreset("focus","system","acrylic",92,true,true,true),
        new VisualPreset("broadcast","dark","blur",96,false,true,true),
        new VisualPreset("accessible","Contrast","gradient",100,false,false,false)
    };
}
