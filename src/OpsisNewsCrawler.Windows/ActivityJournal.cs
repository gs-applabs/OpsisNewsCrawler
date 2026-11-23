using OpsisNewsCrawler.Core;
using OpsisNewsCrawler.Plugin.Abstractions;

namespace OpsisNewsCrawler.Windows;

/// <summary>
/// One local bounded journal for standalone and hosted Crawler. Writes at most every five
/// minutes plus shutdown (and only when content changed), avoiding SSD write amplification.
/// </summary>
internal sealed class ActivityJournal
{
    private const string CacheName = "activity-p2";
    private readonly ActivityTimeline timeline = new();
    private readonly IDisposable subscription;
    private readonly System.Threading.Timer persistence;
    private int dirty;

    public static ActivityJournal Shared { get; } = new();

    private ActivityJournal()
    {
        var previous = DataCache.Load<List<ActivityItem>>(CacheName)?.Data;
        if (previous is not null) timeline.Load(previous);
        subscription = SuiteEventHub.Subscribe(OnSuiteEvent, replay:true);
        persistence = new System.Threading.Timer(_ => Flush(), null,
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public IReadOnlyList<ActivityItem> Snapshot(string? kind = null, int days = 0) => timeline.Snapshot(kind,days);

    private void OnSuiteEvent(SuiteEvent item)
    {
        var record = new ActivityItem($"{item.Origin}\u001f{item.Kind}\u001f{item.Key}",
            item.Kind.ToString(),item.Origin,item.Title,item.At,item.Url,item.Summary);
        if (timeline.Add(record)) Interlocked.Exchange(ref dirty,1);
    }

    /// <summary>Controlled flush; DataCache skips writes for equal snapshots.</summary>
    public void Flush()
    {
        if (Interlocked.Exchange(ref dirty,0) == 0) return;
        DataCache.Save(CacheName, timeline.Snapshot().ToList());
    }
}
