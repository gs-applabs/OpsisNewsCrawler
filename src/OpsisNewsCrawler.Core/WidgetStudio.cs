namespace OpsisNewsCrawler.Core;

/// <summary>A bounded, local presentation profile for an existing Opsis widget.
/// The host still owns widget placement, windows, resize, monitor and lifecycle.
/// Crawler owns only the text rows; no parallel wallpaper/DeskStrip renderer.</summary>
public sealed class WidgetStudioProfile
{
    public string Layout { get; set; } = "headlines"; // headlines or context
    public int MaxItems { get; set; } = 12;
    public bool ShowSource { get; set; }
    public bool ShowTime { get; set; }
}

public static class WidgetStudio
{
    public static readonly IReadOnlyList<string> Kinds =
        Array.AsReadOnly(new[] { "news", "markets", "weather", "breaking", "history" });

    public static WidgetStudioProfile Clean(WidgetStudioProfile? raw) => new()
    {
        Layout = raw?.Layout is "context" or "gallery" ? raw.Layout : "headlines",
        MaxItems = Math.Clamp(raw?.MaxItems ?? 12, 3, 30),
        ShowSource = raw?.ShowSource ?? false,
        ShowTime = raw?.ShowTime ?? false
    };

    public static Dictionary<string, WidgetStudioProfile> CleanAll(
        IDictionary<string, WidgetStudioProfile>? configured)
    {
        var result = new Dictionary<string, WidgetStudioProfile>(StringComparer.OrdinalIgnoreCase);
        if (configured is null) return result;
        foreach (var key in Kinds)
        {
            var candidate = configured.FirstOrDefault(kv =>
                string.Equals(key, kv.Key, StringComparison.OrdinalIgnoreCase));
            if (candidate.Key is not null)
            {
                var clean = Clean(candidate.Value);
                if (key == "history") clean.ShowTime = false; // historical year != publication time
                if (!CanUseGallery(key) && clean.Layout == "gallery") clean.Layout = "headlines";
                result[key] = clean;
            }
        }
        return result;
    }

    public static bool CanUseGallery(string kind) =>
        kind is "news" or "breaking" or "history";

    public static WidgetStudioProfile Resolve(
        IDictionary<string, WidgetStudioProfile>? configured, string kind)
    {
        if (!Kinds.Contains(kind, StringComparer.OrdinalIgnoreCase)) return Clean(null);
        return CleanAll(configured).TryGetValue(kind, out var p) ? p : Clean(null);
    }

    public static string Title(string kind, bool greek) => (kind.ToLowerInvariant(), greek) switch
    {
        ("news", true) => "Ειδήσεις", ("news", false) => "News",
        ("markets", true) => "Αγορές", ("markets", false) => "Markets",
        ("weather", true) => "Καιρός", ("weather", false) => "Weather",
        ("breaking", true) => "Έκτακτες", ("breaking", false) => "Breaking",
        ("history", true) => "Σαν Σήμερα", ("history", false) => "On This Day",
        _ => kind
    };

    /// <summary>Deterministic, text-only gallery cards. Summaries and metadata
    /// are clipped and stripped of control characters; no remote images or HTML
    /// are fetched or evaluated as part of desktop widget rendering.</summary>
    public static string GalleryCardText(string? title, string? summary, string? source,
        DateTimeOffset publishedAt, WidgetStudioProfile? configured,
        bool greek, bool breaking, bool history)
    {
        var p = Clean(configured);
        var heading = CleanText(title, 110);
        var teaser = CleanText(summary, 170);
        if (string.Equals(teaser, heading, StringComparison.OrdinalIgnoreCase))
            teaser = "";
        var metadata = new List<string>(2);
        if (p.ShowSource && !string.IsNullOrWhiteSpace(source))
            metadata.Add(CleanText(source, 38));
        if (p.ShowTime && !history && publishedAt != DateTimeOffset.MinValue &&
            publishedAt <= DateTimeOffset.UtcNow.AddMinutes(5) &&
            DateTimeOffset.UtcNow - publishedAt <= TimeSpan.FromDays(30))
            metadata.Add(publishedAt.ToLocalTime().ToString("HH:mm"));
        var lead = breaking ? (greek ? "ΕΚΤΑΚΤΟ  ·  " : "BREAKING  ·  ") : "";
        var lines = new List<string> { lead + heading };
        if (teaser.Length != 0) lines.Add(teaser);
        if (metadata.Count != 0) lines.Add(string.Join("  ·  ", metadata));
        return string.Join(Environment.NewLine, lines);
    }

    private static string CleanText(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var s = new string(value.Trim().Select(ch => char.IsControl(ch) ? ' ' : ch).ToArray());
        s = string.Join(' ', s.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return s.Length > max ? s[..(max - 1)] + "…" : s;
    }

    /// <summary>One text row. Times are explicit local clock times, never
    /// ambiguous 'today' labels for historical events or fake verification scores.</summary>
    public static string FormatLine(string? title, string? source,
        DateTimeOffset publishedAt, WidgetStudioProfile? configured, bool allowTime = true)
    {
        var p = Clean(configured);
        var headline = (title ?? "").Trim().Replace('\r', ' ').Replace('\n', ' ');
        if (headline.Length > 160) headline = headline[..159] + "…";
        if (p.Layout == "headlines") return headline;
        var metadata = new List<string>(2);
        if (p.ShowSource && !string.IsNullOrWhiteSpace(source))
        {
            var label = source.Trim().Replace('\r', ' ').Replace('\n', ' ');
            metadata.Add(label.Length > 38 ? label[..38] + "…" : label);
        }
        if (allowTime && p.ShowTime && publishedAt != DateTimeOffset.MinValue &&
            publishedAt <= DateTimeOffset.UtcNow.AddMinutes(5) &&
            DateTimeOffset.UtcNow - publishedAt <= TimeSpan.FromDays(30))
            metadata.Add(publishedAt.ToLocalTime().ToString("HH:mm"));
        var prefix = string.Join(" · ", metadata);
        return prefix.Length == 0 ? headline : prefix + "  —  " + headline;
    }
}
