namespace OpsisNewsCrawler.Core;

/// <summary>One drawn entry of a strip: what it is, where it starts and how wide it is (pixels).</summary>
public readonly record struct StripEntry(string Id, int X, int Width);

/// <summary>
/// Keeps the reader's place when the strip is rebuilt after a refresh. New headlines come in front, prices change width and old stories leave,
/// so the same pixel position would show something else (a visible hiccup). The entry at the left edge is found again in the new strip and the
/// position is set so that it stays exactly where it was; if it left, the next entry that survived takes its place.
/// </summary>
public static class ScrollAnchor
{
    /// <summary>
    /// What an entry IS, independent of its changing text: the key of weather and plugin lines, the address of an article, the instrument of a quote
    /// (its price changes, the instrument does not), otherwise source and title.
    /// </summary>
    public static string IdentityOf(NewsItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!string.IsNullOrEmpty(item.Key)) return item.Kind + "|k|" + item.Key;
        if (!string.IsNullOrEmpty(item.Url)) return item.Kind + "|u|" + item.Url;
        if (item.Kind is NewsItemKind.Quote or NewsItemKind.Weather) return item.Kind + "|i|" + item.Source + "|" + item.Summary;
        return item.Kind + "|t|" + item.Source + "|" + item.Title;
    }

    /// <summary>
    /// The new scroll position. <paramref name="offset"/> is the position in the old strip (the pixel at the left edge), the loops are the widths of
    /// one full round of the old and the new strip. The result is within the new loop.
    /// </summary>
    public static double Relocate(IReadOnlyList<StripEntry> before, int beforeLoop, double offset, IReadOnlyList<StripEntry> after, int afterLoop)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        if (afterLoop <= 0 || after.Count == 0 || !double.IsFinite(offset)) return 0;
        if (before.Count == 0 || beforeLoop <= 0) return Wrap(offset, afterLoop);

        var position = Wrap(offset, beforeLoop);
        var first = 0;
        for (var i = 0; i < before.Count; i++)
            if (before[i].X <= position) first = i; else break;

        // The new strip may contain the same identity more than once (the same line in two feeds): match by the order of occurrence.
        var lookup = new Dictionary<string, List<StripEntry>>(StringComparer.Ordinal);
        foreach (var e in after)
        {
            if (!lookup.TryGetValue(e.Id, out var list)) lookup[e.Id] = list = new List<StripEntry>();
            list.Add(e);
        }

        for (var k = 0; k < before.Count; k++)
        {
            var index = (first + k) % before.Count;
            var old = before[index];
            if (!lookup.TryGetValue(old.Id, out var matches)) continue;
            var ordinal = 0;
            for (var j = 0; j < index; j++) if (before[j].Id == old.Id) ordinal++;
            var found = matches[Math.Min(ordinal, matches.Count - 1)];
            double delta;
            if (k == 0)
                delta = Math.Clamp(position - old.X, 0, Math.Max(0, found.Width - 1));   // the same place inside the same entry
            else
                delta = -Wrap(old.X - position, beforeLoop);                             // the surviving entry keeps its distance from the left edge
            return Wrap(found.X + delta, afterLoop);
        }
        return Wrap(offset, afterLoop);   // nothing in common (a different list): keep the pixel position
    }

    private static double Wrap(double value, int loop)
    {
        var v = value % loop;
        return v < 0 ? v + loop : v;
    }
}
