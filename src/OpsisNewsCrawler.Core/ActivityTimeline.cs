namespace OpsisNewsCrawler.Core;

/// <summary>A safe, bounded record for the user's local news/market/weather/history activity.</summary>
public sealed record ActivityItem(string Key, string Kind, string Source, string Title,
    DateTimeOffset At, string? Url, string? Summary);

/// <summary>
/// Local journal: sorted, bounded and deduplicated. No telemetry, network I/O or timers.
/// The Windows adapter persists snapshots only at controlled intervals and shutdown.
/// </summary>
public sealed class ActivityTimeline
{
    public const int MaxEntries = 500;
    private readonly object gate = new();
    private readonly List<ActivityItem> entries = new();

    public int Count { get { lock (gate) return entries.Count; } }

    public bool Add(ActivityItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Key) || string.IsNullOrWhiteSpace(item.Title)) return false;
        lock (gate)
        {
            var index = entries.FindIndex(x => string.Equals(x.Key,item.Key,StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                var previous = entries[index];
                if (previous.Title == item.Title && previous.Summary == item.Summary &&
                    previous.Url == item.Url && previous.Kind == item.Kind) return false;
                entries.RemoveAt(index);
            }
            entries.Add(item);
            entries.Sort((a,b) => b.At.CompareTo(a.At));
            if (entries.Count > MaxEntries) entries.RemoveRange(MaxEntries,entries.Count-MaxEntries);
            return true;
        }
    }

    public void Load(IEnumerable<ActivityItem> saved)
    {
        ArgumentNullException.ThrowIfNull(saved);
        lock (gate)
        {
            entries.Clear();
            foreach (var item in saved.Where(x => !string.IsNullOrWhiteSpace(x.Key) &&
                                                    !string.IsNullOrWhiteSpace(x.Title))
                                      .OrderByDescending(x => x.At).Take(MaxEntries))
                if (!entries.Any(x => string.Equals(x.Key,item.Key,StringComparison.OrdinalIgnoreCase)))
                    entries.Add(item);
        }
    }

    public IReadOnlyList<ActivityItem> Snapshot(string? kind = null, int days = 0, DateTimeOffset? now = null)
    {
        lock (gate)
        {
            var cutoff = days > 0 ? (now ?? DateTimeOffset.Now).AddDays(-Math.Min(3650,days)) : DateTimeOffset.MinValue;
            return entries.Where(x => (string.IsNullOrWhiteSpace(kind) || x.Kind.Equals(kind,StringComparison.OrdinalIgnoreCase)) &&
                                      x.At >= cutoff).ToArray();
        }
    }
}
