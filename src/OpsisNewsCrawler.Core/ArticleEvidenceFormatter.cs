namespace OpsisNewsCrawler.Core;

/// <summary>
/// Supplemental source visibility. A source listed here is an RSS attribution,
/// not independently verified proof and never a truthfulness probability.
/// </summary>
public static class ArticleEvidenceFormatter
{
    public static string Summary(NewsItem item, bool greek)
    {
        if (item.Kind != NewsItemKind.News || item.StoryReports is not { Count: > 0 }) return "";
        var original = PublisherIdentity.Canonical(BreakingEngine.Publisher(item));
        var sources = item.StoryReports
            .Select(r => PublisherIdentity.Canonical(BreakingEngine.RootHost(r.Host, r.Source)))
            .Where(s => s.Length > 0 && !string.Equals(s, original, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4).ToArray();
        if (sources.Length == 0) return greek
            ? "Επιπλέον αναφορές από την ίδια εκδοτική ομάδα (όχι ανεξάρτητη επιβεβαίωση)."
            : "Additional coverage from the same newsroom (not independent confirmation).";
        var intro = greek ? "Άλλες εκδοτικές ομάδες: " : "Other newsrooms: ";
        var suffix = greek ? " · Οι πηγές δεν αποτελούν απόδειξη ακρίβειας." :
            " · Multiple sources do not prove accuracy.";
        return intro + string.Join(", ", sources) + suffix;
    }
}
