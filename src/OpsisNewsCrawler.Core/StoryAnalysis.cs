using System.Text.RegularExpressions;

namespace OpsisNewsCrawler.Core;

/// <summary>One report of a story in time order (N03).</summary>
public sealed record StoryTimelineEntry(DateTimeOffset At, string Source, string Title, string Url);

/// <summary>Something that differs between the reports of one story, for a person to judge (N04). Never a verdict.</summary>
public sealed record StoryDifference(string Kind, IReadOnlyList<string> Values, IReadOnlyList<string> Sources);

/// <summary>Reports that look like one text republished by several outlets (N05): the same headline or summary, or a named agency.</summary>
public sealed record CommonOrigin(string? Agency, IReadOnlyList<NewsItem> Reports);

/// <summary>Pure functions over a <see cref="StoryCluster"/> (the existing story explorer). They use only what the feeds delivered.</summary>
public static class StoryAnalysis
{
    private static readonly string[] Agencies = { "Reuters", "Associated Press", "AP", "AFP", "Bloomberg", "ΑΠΕ-ΜΠΕ", "ΑΠΕ", "ΜΠΕ", "ANA-MPA", "ΚΥΠΕ", "CNA", "Anadolu", "DPA", "ANSA", "EFE", "TASS", "Xinhua" };

    /// <summary>The reports in the order they were published (N03). Reports without a real time (min value) are left out, duplicates of the same link are folded.</summary>
    public static IReadOnlyList<StoryTimelineEntry> Timeline(StoryCluster cluster)
    {
        ArgumentNullException.ThrowIfNull(cluster);
        return cluster.Reports.Where(r => r.PublishedAt > DateTimeOffset.MinValue.AddYears(1))
            .GroupBy(r => string.IsNullOrWhiteSpace(r.Url) ? r.Source + "|" + r.Title : r.Url, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderBy(r => r.PublishedAt).First())
            .OrderBy(r => r.PublishedAt).ThenBy(r => r.Source, StringComparer.OrdinalIgnoreCase)
            .Select(r => new StoryTimelineEntry(r.PublishedAt, r.Source, r.Title, r.Url)).ToArray();
    }

    /// <summary>
    /// Numbers that differ between the headlines of different publishers (for example "5 dead" and "7 dead"). The numbers are shown side by side with their sources;
    /// the program does not say which one is right.
    /// </summary>
    public static IReadOnlyList<StoryDifference> Differences(StoryCluster cluster)
    {
        ArgumentNullException.ThrowIfNull(cluster);
        var byPublisher = cluster.Reports.Select(r => (Publisher: PublisherIdentity.Canonical(BreakingEngine.Publisher(r)), Item: r))
            .Where(x => x.Publisher.Length > 0).GroupBy(x => x.Publisher).Select(g => g.First()).ToList();
        if (byPublisher.Count < 2) return Array.Empty<StoryDifference>();

        var numbersOf = byPublisher.Select(x => (x.Publisher, Numbers: Numbers(x.Item.Title))).ToList();
        var common = numbersOf.Select(x => x.Numbers).Aggregate((a, b) => a.Intersect(b).ToHashSet());
        var all = numbersOf.SelectMany(x => x.Numbers).Distinct().ToList();
        // A number that every publisher uses is not a difference; a number that only some use is, when the others use another number.
        var odd = all.Where(n => !common.Contains(n)).OrderBy(n => n, StringComparer.Ordinal).ToList();
        if (odd.Count < 2 || numbersOf.Any(x => x.Numbers.Count == 0)) return Array.Empty<StoryDifference>();
        var sources = numbersOf.Where(x => x.Numbers.Any(odd.Contains)).Select(x => x.Publisher).Distinct().ToArray();
        return new[] { new StoryDifference("number", odd, sources) };
    }

    /// <summary>Groups reports with the same headline or summary from different publishers, or that name the same agency (N05). Attribution is kept as the feeds gave it.</summary>
    public static IReadOnlyList<CommonOrigin> CommonOrigins(StoryCluster cluster)
    {
        ArgumentNullException.ThrowIfNull(cluster);
        var result = new List<CommonOrigin>();
        var used = new HashSet<NewsItem>();

        foreach (var agency in Agencies)
        {
            var pattern = new Regex(@"(?<![\p{L}\p{N}])" + Regex.Escape(agency) + @"(?![\p{L}\p{N}])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
            var reports = cluster.Reports.Where(r => !used.Contains(r) && Safe(pattern, (r.Title ?? "") + " " + (r.Summary ?? ""))).ToList();
            if (Publishers(reports) >= 2) { foreach (var r in reports) used.Add(r); result.Add(new CommonOrigin(agency, reports)); }
        }
        var same = cluster.Reports.Where(r => !used.Contains(r)).GroupBy(r => Normal(string.IsNullOrWhiteSpace(r.Summary) ? r.Title : r.Summary))
            .Where(g => g.Key.Length >= 40 && Publishers(g) >= 2);
        foreach (var g in same) result.Add(new CommonOrigin(null, g.ToList()));
        return result;
    }

    private static bool Safe(Regex pattern, string text)
    {
        try { return pattern.IsMatch(text); } catch (RegexMatchTimeoutException) { return false; }
    }

    private static int Publishers(IEnumerable<NewsItem> items) =>
        items.Select(i => PublisherIdentity.Canonical(BreakingEngine.Publisher(i))).Where(p => p.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Count();

    private static string Normal(string? text) => string.Join(' ', HeadlineDedup.Words(text).OrderBy(w => w, StringComparer.Ordinal));

    private static HashSet<string> Numbers(string? title)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(title)) return set;
        foreach (Match m in Regex.Matches(title, @"\d+(?:[.,]\d+)?", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50)))
        {
            var v = m.Value.Replace(',', '.');
            // years and clock times are not counts
            if (v.Length == 4 && int.TryParse(v, out var year) && year is >= 1900 and <= 2100) continue;
            set.Add(v);
        }
        return set;
    }
}
