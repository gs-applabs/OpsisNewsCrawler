using System.Text.Json;

namespace OpsisNewsCrawler.Core;

/// <summary>A headline that changed at the same address (N07), seen only in what the feed delivered.</summary>
public sealed record ArticleChange(string Url, string OldTitle, string NewTitle, bool SummaryChanged, DateTimeOffset SeenAt);

public sealed record ArchivedStory(string Title, string Url, string Source, DateTimeOffset PublishedAt, string? Summary, DateTimeOffset SeenAt);

/// <summary>
/// N39 (and N07): an optional local archive of headlines and summaries for offline search, switched off unless the user turns it on.
/// It keeps at most <see cref="MaxItems"/> stories for <see cref="RetentionDays"/> days, only data the feeds delivered, writes at most once every
/// <see cref="WriteInterval"/> (and when asked to flush) and never fetches anything. Changes of a headline at the same address are recorded as they are seen.
/// </summary>
public sealed class NewsArchive
{
    public const int MaxItems = 5000, RetentionDays = 30, MaxChanges = 200;
    public static readonly TimeSpan WriteInterval = TimeSpan.FromMinutes(10);

    private readonly string path;
    private readonly object gate = new();
    private readonly Dictionary<string, ArchivedStory> stories = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ArticleChange> changes = new();
    private DateTimeOffset lastWrite = DateTimeOffset.MinValue;
    private bool dirty;

    public static string DefaultPath => Path.Combine(
        CrawlerDataPaths.Root, "news-archive.json");

    private sealed record File_(List<ArchivedStory> Stories, List<ArticleChange> Changes);

    public NewsArchive(string? path = null)
    {
        this.path = path ?? DefaultPath;
        Load();
    }

    public int Count { get { lock (gate) return stories.Count; } }

    public IReadOnlyList<ArticleChange> Changes { get { lock (gate) return changes.OrderByDescending(c => c.SeenAt).ToArray(); } }

    /// <summary>Adds the new stories, records changed headlines and prunes; writes only when something changed and the interval has passed.</summary>
    public void Observe(IEnumerable<NewsItem> news, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(news);
        lock (gate)
        {
            foreach (var item in news.Where(n => n.Kind == NewsItemKind.News && NetGuard.IsPublicHttpUrl(n.Url) && !string.IsNullOrWhiteSpace(n.Title)).Take(MaxItems))
            {
                var summary = item.Summary is { Length: > 600 } s ? s[..600] : item.Summary;
                if (stories.TryGetValue(item.Url, out var old))
                {
                    var titleChanged = !string.Equals(old.Title, item.Title, StringComparison.Ordinal);
                    var summaryChanged = !string.Equals(old.Summary ?? "", summary ?? "", StringComparison.Ordinal);
                    if (!titleChanged && !summaryChanged) continue;
                    changes.Add(new ArticleChange(item.Url, old.Title, item.Title, summaryChanged, now));
                    if (changes.Count > MaxChanges) changes.RemoveRange(0, changes.Count - MaxChanges);
                }
                stories[item.Url] = new ArchivedStory(item.Title, item.Url, item.Source, item.PublishedAt, summary, now);
                dirty = true;
            }
            Prune(now);
            if (dirty && now - lastWrite >= WriteInterval) Write(now);
        }
    }

    /// <summary>The same search as the live news (N10), over the archive.</summary>
    public IReadOnlyList<NewsItem> Search(NewsQuery query)
    {
        List<NewsItem> all;
        lock (gate) all = stories.Values.Select(a => new NewsItem(a.Title, a.Url, a.Source, a.PublishedAt, a.Summary)).ToList();
        return NewsSearch.Find(all, query);
    }

    public void Flush(DateTimeOffset now) { lock (gate) if (dirty) Write(now); }

    /// <summary>Removes everything, also from the disk (the user switched the archive off).</summary>
    public void Clear()
    {
        lock (gate)
        {
            stories.Clear(); changes.Clear(); dirty = false;
            try { File.Delete(path); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    private void Prune(DateTimeOffset now)
    {
        var cutoff = now.AddDays(-RetentionDays);
        foreach (var key in stories.Where(p => p.Value.SeenAt < cutoff).Select(p => p.Key).ToList()) { stories.Remove(key); dirty = true; }
        if (changes.RemoveAll(c => c.SeenAt < cutoff) > 0) dirty = true;
        if (stories.Count <= MaxItems) return;
        foreach (var key in stories.OrderBy(p => p.Value.SeenAt).Take(stories.Count - MaxItems).Select(p => p.Key).ToList()) { stories.Remove(key); dirty = true; }
    }

    private void Write(DateTimeOffset now)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            SafeFile.WriteAllText(path, JsonSerializer.Serialize(new File_(stories.Values.ToList(), changes)));
            lastWrite = now;
            dirty = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // An archive that cannot be written must not disturb the ticker; it is retried later.
        }
    }

    private void Load()
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > 32 * 1024 * 1024) return;
            var data = JsonSerializer.Deserialize<File_>(File.ReadAllText(path));
            if (data is null) return;
            foreach (var s in (data.Stories ?? new()).Where(s => s is not null && !string.IsNullOrEmpty(s.Url) && !string.IsNullOrEmpty(s.Title)).Take(MaxItems))
                stories[s.Url] = s;
            changes.AddRange((data.Changes ?? new()).Where(c => c is not null).TakeLast(MaxChanges));
            Prune(DateTimeOffset.UtcNow);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // A damaged archive is simply empty.
        }
    }
}
