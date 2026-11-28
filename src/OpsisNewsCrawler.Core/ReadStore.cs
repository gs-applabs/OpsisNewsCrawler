using System.Text.Json;

namespace OpsisNewsCrawler.Core;

/// <summary>
/// Remembers which articles the user already opened (URL → time), in %AppData%\OpsisNewsCrawler\read.json.
/// Entries older than 14 days are dropped. Use from the UI thread.
/// </summary>
public sealed class ReadStore
{
    private static readonly TimeSpan Retention = TimeSpan.FromDays(14);
    /// <summary>N38: the newest links only; a heavy reader never grows the file without end.</summary>
    public const int MaxEntries = 5000;

    private readonly string path;
    private readonly Dictionary<string, DateTimeOffset> read = new(StringComparer.OrdinalIgnoreCase);

    public static string DefaultPath => Path.Combine(
        CrawlerDataPaths.Root, "read.json");

    public ReadStore(string? path = null)
    {
        this.path = path ?? DefaultPath;
        Load();
    }

    public int Count => read.Count;

    public bool IsRead(string? url) => !string.IsNullOrEmpty(url) && read.ContainsKey(url);

    public void MarkRead(string? url)
    {
        if (string.IsNullOrEmpty(url)) return;
        read[url] = DateTimeOffset.UtcNow;
        if (read.Count > MaxEntries) Trim();
        Save();
    }

    public void Clear()
    {
        read.Clear();
        Save();
    }

    /// <summary>Keeps the newest 90 % of the limit, so trimming does not happen on every click.</summary>
    private void Trim()
    {
        foreach (var key in read.OrderBy(p => p.Value).Take(read.Count - MaxEntries * 9 / 10).Select(p => p.Key).ToList()) read.Remove(key);
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(path)) return;
            var data = JsonSerializer.Deserialize<Dictionary<string, DateTimeOffset>>(File.ReadAllText(path));
            if (data is null) return;
            var cutoff = DateTimeOffset.UtcNow - Retention;
            foreach (var (url, when) in data)
                if (when >= cutoff) read[url] = when;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // A damaged file just means nothing is marked as read.
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            SafeFile.WriteAllText(path, JsonSerializer.Serialize(read));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
