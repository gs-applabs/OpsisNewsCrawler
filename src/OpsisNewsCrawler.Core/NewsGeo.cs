namespace OpsisNewsCrawler.Core;

/// <summary>A country a story names, taken from the text only (N08). Never the location of the reader.</summary>
public sealed record GeoHint(string CountryCode, string CountryName, string ContinentKey, string MatchedText);

/// <summary>
/// N08: a geographic category from explicit country names in the title and summary, using the country list the weather pages already have.
/// When no country is named, or two different countries are named, or the name is ambiguous (Georgia, Jordan, Chad...), the answer is "unknown" (null): no guess,
/// no IP address, no GPS.
/// </summary>
public static class NewsGeo
{
    private static readonly HashSet<string> Ambiguous = new(StringComparer.Ordinal)
    { "georgia", "jordan", "chad", "guinea", "niger", "γεωργια", "τσαντ", "γουινεα" };

    private static readonly Lazy<IReadOnlyList<(string Code, string Name, string Continent, string[] Names)>> Catalog = new(() =>
    {
        var list = new List<(string, string, string, string[])>();
        foreach (var continent in WorldPlaces.Continents)
            foreach (var country in continent.Countries)
            {
                var names = new[] { country.NameEn, country.NameEl }.Where(n => !string.IsNullOrWhiteSpace(n))
                    .Select(n => " " + Flat(n) + " ").Where(n => n.Trim().Length >= 4 && !Ambiguous.Contains(n.Trim())).Distinct().ToArray();
                if (names.Length > 0) list.Add((country.Code, country.NameEn, continent.Key, names));
            }
        // The weather catalog leaves out the home country (it has its own list of Greek cities): add it here.
        if (!list.Any(c => c.Item1 == "GR"))
            list.Add(("GR", "Greece", Continents.Key(Continent.Europe), new[] { " " + Flat("Greece") + " ", " " + Flat("Ελλάδα") + " ", " " + Flat("Ελλάδας") + " " }));
        return list;
    });

    public static GeoHint? Hint(NewsItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var text = " " + Flat((item.Title ?? "") + " " + (item.Summary ?? "")) + " ";
        GeoHint? found = null;
        foreach (var (code, name, continent, names) in Catalog.Value)
        {
            var hit = names.FirstOrDefault(n => text.Contains(n, StringComparison.Ordinal));
            if (hit is null) continue;
            if (found is not null && found.CountryCode != code) return null;   // two countries: no single place, so unknown
            found = new GeoHint(code, name, continent, hit.Trim());
        }
        return found;
    }

    /// <summary>Lower case, accents and punctuation removed, single spaces: the same flattening for the names and for the text.</summary>
    private static string Flat(string text)
    {
        var chars = Breaking.Normalize(text).ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? (c == 'ς' ? 'σ' : c) : ' ').ToArray();
        return string.Join(' ', new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
