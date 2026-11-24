namespace OpsisNewsCrawler.Core;

/// <summary>
/// Opt-in, settings-only templates for the five existing Opsis Crawler widgets.
/// Applying a template must not create a second widget host, theme engine,
/// wallpaper renderer, scheduler or extra JSON file.
/// </summary>
public static class WidgetStudioPresets
{
    public static readonly IReadOnlyList<string> Keys = Array.AsReadOnly(
        new[] { "newsroom", "financial", "weather-hazards", "greece-today", "readable" });

    public static string Title(string key, bool greek) => (key, greek) switch
    {
        ("newsroom", true) => "Newsroom / Ειδήσεις",
        ("newsroom", false) => "Newsroom",
        ("financial", true) => "Financial Desk / Αγορές",
        ("financial", false) => "Financial Desk",
        ("weather-hazards", true) => "Καιρός και Προειδοποιήσεις",
        ("weather-hazards", false) => "Weather & Hazards",
        ("greece-today", true) => "Ελλάδα Σήμερα",
        ("greece-today", false) => "Greece Today",
        ("readable", true) => "Ευανάγνωστη προβολή",
        ("readable", false) => "Easy Read",
        _ => key
    };

    public static Dictionary<string, WidgetStudioProfile> Create(string key)
    {
        if (!Keys.Contains(key, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Unknown widget profile", nameof(key));
        var profiles = WidgetStudio.Kinds.ToDictionary(
            k => k, _ => new WidgetStudioProfile(), StringComparer.OrdinalIgnoreCase);
        void Set(string kind, string layout, int max, bool source = false, bool time = false)
        {
            profiles[kind] = new WidgetStudioProfile
            {
                Layout = layout, MaxItems = max, ShowSource = source, ShowTime = time
            };
        }
        switch (key.ToLowerInvariant())
        {
            case "newsroom":
                Set("news", "gallery", 12, source: true, time: true);
                Set("breaking", "gallery", 8, source: true);
                Set("history", "gallery", 8, source: true);
                break;
            case "financial":
                Set("news", "context", 8, source: true, time: true);
                Set("markets", "context", 20, source: true);
                Set("breaking", "context", 6, source: true, time: true);
                Set("weather", "context", 4);
                Set("history", "headlines", 4);
                break;
            case "weather-hazards":
                Set("weather", "context", 12, source: true, time: true);
                Set("breaking", "gallery", 8, source: true);
                Set("news", "context", 8, source: true);
                break;
            case "greece-today":
                Set("news", "gallery", 10, source: true, time: true);
                Set("breaking", "gallery", 8, source: true);
                Set("history", "gallery", 12, source: true);
                Set("weather", "context", 8, source: true);
                break;
            case "readable":
                Set("news", "gallery", 5, source: true);
                Set("breaking", "gallery", 5, source: true);
                Set("history", "gallery", 5, source: true);
                Set("markets", "context", 5, source: false);
                Set("weather", "context", 5);
                break;
        }
        return WidgetStudio.CleanAll(profiles);
    }
}
