namespace OpsisNewsCrawler.Plugin.Abstractions;

/// <summary>
/// Optional in-process events for the Opsis family. No change to the host's existing interfaces.
/// Consumers must share the same contract DLL instance and marshal updates to their own UI thread.
/// </summary>
public enum SuiteEventKind { News, Breaking, Market, Weather, Calendar, Hazard }

public sealed record SuiteEvent(
    SuiteEventKind Kind, string Origin, string Key, string Title,
    DateTimeOffset At, string? Url = null, string? Summary = null,
    string? ImageId = null, int? HistoricalYear = null);

public static class SuiteEventHub
{
    private static readonly object Gate = new();
    private static readonly Dictionary<int, Action<SuiteEvent>> Subscribers = new();
    private static readonly Dictionary<string, SuiteEvent> Latest = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Queue<string> Insertion = new();
    private const int Limit = 256;
    private static int nextId;

    public static IDisposable Subscribe(Action<SuiteEvent> onEvent, bool replay = true)
    {
        ArgumentNullException.ThrowIfNull(onEvent);
        int id;
        SuiteEvent[] snapshot;
        lock (Gate)
        {
            id = ++nextId;
            Subscribers.Add(id, onEvent);
            snapshot = replay ? Latest.Values.OrderBy(e => e.At).ToArray() : Array.Empty<SuiteEvent>();
        }
        foreach (var item in snapshot) SafeInvoke(onEvent, item);
        return new Subscription(id);
    }

    public static void Publish(SuiteEvent item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (string.IsNullOrWhiteSpace(item.Origin) || string.IsNullOrWhiteSpace(item.Key)
            || string.IsNullOrWhiteSpace(item.Title)) return;
        var identity = $"{item.Origin}\u001f{item.Kind}\u001f{item.Key}";
        Action<SuiteEvent>[] handlers;
        lock (Gate)
        {
            if (Latest.TryGetValue(identity, out var existing)
                && existing.Title == item.Title && existing.Url == item.Url
                && existing.Summary == item.Summary && existing.ImageId == item.ImageId) return;
            if (!Latest.ContainsKey(identity)) Insertion.Enqueue(identity);
            Latest[identity] = item;
            while (Insertion.Count > Limit) Latest.Remove(Insertion.Dequeue());
            handlers = Subscribers.Values.ToArray();
        }
        foreach (var handler in handlers) SafeInvoke(handler, item);
    }

    public static IReadOnlyList<SuiteEvent> Snapshot(SuiteEventKind? kind = null)
    {
        lock (Gate) return Latest.Values.Where(x => kind is null || x.Kind == kind.Value)
            .OrderByDescending(x => x.At).ToArray();
    }

    private static void SafeInvoke(Action<SuiteEvent> handler, SuiteEvent item)
    {
        try { handler(item); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Suite event observer failed: {ex}"); }
    }

    private sealed class Subscription : IDisposable
    {
        private int id;
        internal Subscription(int id) => this.id = id;
        public void Dispose()
        {
            var prior = Interlocked.Exchange(ref id, 0);
            if (prior == 0) return;
            lock (Gate) Subscribers.Remove(prior);
        }
    }
}
