using System.Net;
using System.Text.RegularExpressions;

namespace OpsisNewsCrawler.Core;

/// <summary>A bounded, local presentation profile for an existing Opsis widget.
/// The host still owns widget placement, windows, resize, monitor and lifecycle.
/// Crawler owns only the text rows; no parallel wallpaper/DeskStrip renderer.</summary>
public sealed class WidgetStudioProfile
{
    public string Layout { get; set; } = "headlines"; // headlines or context
    public int MaxItems { get; set; } = 12;
    public int FontScalePercent { get; set; } = 100;
    public bool ShowSource { get; set; }
    public bool ShowTime { get; set; }
    /// <summary>"theme" is the existing Crawler palette; "host" is the
    /// Opsis-provided accent when running as a trusted plugin. Standalone uses
    /// the local accent, without opening another settings file or process.</summary>
    public string AccentMode { get; set; } = "theme";
    /// <summary>Existing user-configured RSS category ID. Empty means the
    /// active ticker category. This is NOT IP, GPS or inferred geolocation.</summary>
    public string NewsCategoryId { get; set; } = "";
}

public static class WidgetStudio
{
    public static readonly IReadOnlyList<string> Kinds =
        Array.AsReadOnly(new[] { "news", "markets", "weather", "breaking", "history" });

    public static WidgetStudioProfile Clean(WidgetStudioProfile? raw) => new()
    {
        Layout = raw?.Layout is "context" or "gallery" or "market-cards" ? raw.Layout : "headlines",
        MaxItems = Math.Clamp(raw?.MaxItems ?? 12, 3, 30),
        FontScalePercent = Math.Clamp(raw?.FontScalePercent ?? 100, 80, 160),
        ShowSource = raw?.ShowSource ?? false,
        ShowTime = raw?.ShowTime ?? false,
        AccentMode = raw?.AccentMode == "host" ? "host" : "theme",
        NewsCategoryId = CleanCategoryId(raw?.NewsCategoryId)
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
                if (key != "news") clean.NewsCategoryId = "";
                if (!CanUseGallery(key) && clean.Layout == "gallery") clean.Layout = "headlines";
                if (key != "markets" && clean.Layout == "market-cards") clean.Layout = "headlines";
                result[key] = clean;
            }
        }
        return result;
    }

    public static string CleanCategoryId(string? categoryId)
    {
        if (string.IsNullOrWhiteSpace(categoryId)) return "";
        var text = categoryId.Trim();
        return text.Length is >= 1 and <= 64 &&
            text.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')
            ? text : "";
    }

    public static bool CanUseGallery(string kind) =>
        kind is "news" or "breaking" or "history";

    public static bool CanUseMarketCards(string kind) =>
        string.Equals(kind, "markets", StringComparison.OrdinalIgnoreCase);

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

    /// <summary>
    /// A market card from the exact NewsItem produced by the existing QuoteService.
    /// Delayed/daily/change-from-open labels are already part of NewsItem.Title,
    /// so do not discard those distinctions or claim that a quote is realtime.
    /// No HTTP, trading APIs, portfolio mutations or second quote scheduler.
    /// </summary>
    public static string MarketCardText(string? instrument, string? quoteText,
        DateTimeOffset asOf, int trend, WidgetStudioProfile? configured, bool greek)
    {
        var p = Clean(configured);
        var symbol = CleanText(instrument, 72);
        var price = CleanText(quoteText, 190);
        var direction = trend switch
        {
            > 0 => "↑ ", < 0 => "↓ ", _ => "• "
        };
        var result = new List<string>();
        if (symbol.Length > 0) result.Add(symbol);
        result.Add(direction + price);
        if (p.ShowTime && asOf != DateTimeOffset.MinValue &&
            asOf <= DateTimeOffset.UtcNow.AddMinutes(5))
            result.Add((greek ? "Τελευταία αναφορά: " : "Last reported: ") +
                asOf.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
        return string.Join(Environment.NewLine, result);
    }

    /// <summary>Deterministic, text-only gallery cards. Summaries and metadata
    /// are clipped and stripped of control characters; no remote images or HTML
    /// are fetched or evaluated as part of desktop widget rendering.</summary>
    public static string GalleryCardText(string? title, string? summary, string? source,
        DateTimeOffset publishedAt, WidgetStudioProfile? configured,
        bool greek, bool breaking, bool history, int relatedReports = 0)
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
        // StoryReports are publication records, NOT fact-check confirmation.
        // Never label them as independently verified sources.
        if (p.ShowSource && relatedReports > 1 && !history)
            metadata.Add($"{Math.Clamp(relatedReports, 2, 99)} " +
                (greek ? "συναφείς καταγραφές" : "related reports"));
        if (metadata.Count != 0) lines.Add(string.Join("  ·  ", metadata));
        return string.Join(Environment.NewLine, lines);
    }

    private static string CleanText(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var clipped = value.Length > 4096 ? value[..4096] : value;
        var decoded = WebUtility.HtmlDecode(clipped);
        // Display only safe plain text, never HTML/WebView content in the
        // background desktop widget. The input size and match length are bounded.
        string plain;
        try
        {
            plain = Regex.Replace(decoded, @"<[^<>]{0,256}>", " ",
                RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
        }
        catch (RegexMatchTimeoutException)
        {
            // RSS payload is untrusted. A regex timeout must not crash
            // the always-visible desktop widget.
            plain = decoded.Replace('<', ' ').Replace('>', ' ');
        }
        var s = new string(plain.Trim().Select(ch => char.IsControl(ch) ? ' ' : ch).ToArray());
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
