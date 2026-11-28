using System.Text.Json;
using OpsisNewsCrawler.Plugin.Abstractions;

namespace OpsisNewsCrawler.Windows;

/// <summary>
/// The "On this day" flow of Chronikologio for the bars WITHOUT Opsis. Inside Opsis both programs are plugins of one process and the flow
/// travels through <see cref="PluginFeeds"/>. When they run as separate programs (or only one of them is a plugin) Chronikologio leaves its
/// lines of the next days in a small JSON file in the per-user family folder and this bridge turns today's lines into the same feed.
/// An in-process feed with the same key always wins; the file is only read (never written), bounded, and plain text.
/// </summary>
public static class FamilyFeedBridge
{
    public const string FeedKey = "san-simera";
    public const string FileName = "san-simera.json";
    private const long MaxFileBytes = 256 * 1024;
    private const int MaxLines = 200, MaxLineLength = 300;

    private static readonly object Gate = new();
    private static PluginFeed? ours;
    private static string lastSignature = "";
    private static string lastStamp = "";
    private static bool lastHadData;

    private static string Stamp(string path, DateOnly day)
    {
        try { var i = new FileInfo(path); return i.Exists ? $"{path}|{i.Length}|{i.LastWriteTimeUtc.Ticks}|{day:O}" : $"{path}|missing|{day:O}"; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return $"{path}|error|{day:O}"; }
    }

    /// <summary>The family feeds folder: <c>%LocalAppData%\Opsis\feeds</c>, or the folder named by OPSIS_FEEDS_DIR (tests).</summary>
    public static string DefaultFolder =>
        Environment.GetEnvironmentVariable("OPSIS_FEEDS_DIR") is { Length: > 0 } dir
            ? dir
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Opsis", "feeds");

    /// <summary>
    /// Looks at the file and registers, refreshes or removes the bridged feed. Cheap when nothing changed (one small file read); call it
    /// from an existing timer. Returns true when the feed registry was changed.
    /// </summary>
    public static bool Sync(DateOnly? today = null, string? folder = null)
    {
        lock (Gate)
        {
            var current = PluginFeeds.Find(FeedKey);
            if (current is not null && !ReferenceEquals(current, ours))
            {
                ours = null; lastSignature = ""; lastStamp = ""; lastHadData = false;   // a plugin in this process provides the flow: leave it alone
                return false;
            }
            // The registry may have been cleared independently of this bridge.
            if (current is null && ours is not null)
            {
                ours = null; lastSignature = ""; lastStamp = ""; lastHadData = false;
            }
            var day = today ?? DateOnly.FromDateTime(DateTime.Now);
            var path = Path.Combine(folder ?? DefaultFolder, FileName);
            // Unchanged file on the same day and nothing to change in the registry: no read at all.
            var stamp = Stamp(path, day);
            if (stamp == lastStamp && (ours is not null) == lastHadData) return false;
            var data = Read(path, day);
            lastStamp = stamp; lastHadData = data is not null;
            if (data is null)
            {
                if (ours is null) return false;
                PluginFeeds.Unregister(FeedKey);
                ours = null; lastSignature = "";
                return true;
            }
            var signature = JsonSerializer.Serialize(new { Day = day, data.TitleEl, data.TitleEn, data.Lines });
            if (ours is not null && signature == lastSignature) return false;
            var lines = data.Lines;
            var feed = new PluginFeed(FeedKey, data.TitleEl, data.TitleEn, () => lines.Select(l => new PluginFeedItem(l)).ToList());
            ours = feed; lastSignature = signature;
            PluginFeeds.Register(feed);
            return true;
        }
    }

    /// <summary>Forgets what this bridge registered (tests, shutdown); it does not touch a feed of a plugin.</summary>
    public static void Reset()
    {
        lock (Gate)
        {
            if (ours is not null && ReferenceEquals(PluginFeeds.Find(FeedKey), ours)) PluginFeeds.Unregister(FeedKey);
            ours = null; lastSignature = ""; lastStamp = ""; lastHadData = false;
        }
    }

    private sealed record Parsed(string TitleEl, string TitleEn, IReadOnlyList<string> Lines);

    private static Parsed? Read(string path, DateOnly day)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length is 0 or > MaxFileBytes) return null;
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0) return null;   // no links
            var bytes = OpsisNewsCrawler.Core.BoundedLocalFile.Read(path, (int)MaxFileBytes);
            if (bytes is null) return null;
            using var doc = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 8 });
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("schema", out var schema) || schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out var schemaVersion) || schemaVersion != 1) return null;
            if (!root.TryGetProperty("key", out var key) || key.ValueKind != JsonValueKind.String || key.GetString() != FeedKey) return null;
            if (!root.TryGetProperty("days", out var days) || days.ValueKind != JsonValueKind.Array) return null;
            var wanted = day.ToString("yyyy-MM-dd");
            foreach (var d in days.EnumerateArray().Take(31))
            {
                if (d.ValueKind != JsonValueKind.Object || !d.TryGetProperty("date", out var date) || date.ValueKind != JsonValueKind.String || date.GetString() != wanted) continue;
                if (!d.TryGetProperty("lines", out var linesElement) || linesElement.ValueKind != JsonValueKind.Array) return null;
                var lines = new List<string>();
                foreach (var l in linesElement.EnumerateArray().Take(MaxLines))
                {
                    if (l.ValueKind != JsonValueKind.String) continue;
                    var text = Clean(l.GetString());
                    if (text.Length > 0) lines.Add(text);
                }
                if (lines.Count == 0) return null;
                return new Parsed(Title(root, "titleEl", "Σαν σήμερα"), Title(root, "titleEn", "On this day"), lines);
            }
            return null;   // no entry for today: the file is old, nothing is shown
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    private static string Title(JsonElement root, string name, string fallback) =>
        root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && Clean(v.GetString()) is { Length: > 0 } t ? t[..Math.Min(t.Length, 60)] : fallback;

    private static string Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var chars = text.Select(c => char.IsControl(c) ? ' ' : c).ToArray();
        var clean = string.Join(' ', new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return clean.Length > MaxLineLength ? clean[..MaxLineLength].TrimEnd() + "…" : clean;
    }
}
