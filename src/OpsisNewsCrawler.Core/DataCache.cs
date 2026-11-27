using System.Text.Json;

namespace OpsisNewsCrawler.Core;

/// <summary>A stored copy of some data with the time it was fetched.</summary>
public sealed record Cached<T>(DateTimeOffset At, T Data);

/// <summary>
/// The last good news, prices and weather, kept in %AppData%\OpsisNewsCrawler\cache so that they can still be shown when
/// the network or a provider is down (and right after a start, before the first download has finished).
/// </summary>
public static class DataCache
{
    private static readonly object WriteGate = new();
    private static readonly Dictionary<string, string> WrittenHashes = new(StringComparer.OrdinalIgnoreCase);
    private static long skippedWrites;
    private static long completedWrites;
    public const int MaxCacheFiles = 128;
    public const long MaxCacheBytes = 64L * 1024 * 1024;
    public const int MaxEntryBytes = 16 * 1024 * 1024;
    private static DateTimeOffset lastTrim;

    /// <summary>Counts successful cache writes and skips for the current process; no telemetry is sent anywhere.</summary>
    public static (long Writes, long Skips) WriteDiagnostics =>
        (Interlocked.Read(ref completedWrites), Interlocked.Read(ref skippedWrites));

    public static string Folder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpsisNewsCrawler", "cache");

    /// <summary>Only letters, digits, '-' and '_' are kept: the name ends up in a file name.</summary>
    public static string SafeName(string name)
    {
        var chars = (name ?? "").Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_').ToArray();
        return chars.Length == 0 ? "data" : new string(chars);
    }

    private static string PathOf(string name) => Path.Combine(Folder, SafeName(name) + ".json");

    public static void Save<T>(string name, T data)
    {
        try
        {
            var path = PathOf(name);
            // The timestamp changes on every poll: fingerprint only the payload so unchanged
            // RSS/weather data do not trigger SSD writes several times an hour.
            var payload = JsonSerializer.Serialize(data);
            if (System.Text.Encoding.UTF8.GetByteCount(payload) > MaxEntryBytes - 256) return;
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(payload)));
            lock (WriteGate)
            {
                if (WrittenHashes.TryGetValue(path, out var previous) && previous == hash && File.Exists(path))
                {
                    Interlocked.Increment(ref skippedWrites);
                    return;
                }
                Directory.CreateDirectory(Folder);
                var newFile = !File.Exists(path);
                SafeFile.WriteAllText(path, "{\"At\":" + JsonSerializer.Serialize(DateTimeOffset.UtcNow) + ",\"Data\":" + payload + "}");
                if (WrittenHashes.Count >= MaxCacheFiles * 2) WrittenHashes.Clear();
                WrittenHashes[path] = hash;
                if (newFile || DateTimeOffset.UtcNow - lastTrim >= TimeSpan.FromMinutes(10))
                {
                    TrimGeneratedCache(Folder, path);
                    lastTrim = DateTimeOffset.UtcNow;
                } // on success only; failed writes may be retried
                Interlocked.Increment(ref completedWrites);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // A cache that cannot be written is not a problem for the bar.
        }
    }

    // Only generated cache JSON files, never user configuration, history, archive or backups.
    public static void TrimGeneratedCache(string folder, string? preserve = null)
    {
        lock (WriteGate)
        {
            try
            {
                if (!Directory.Exists(folder)) return;
                var files = new DirectoryInfo(folder).EnumerateFiles("*.json")
                    .Where(f => f.Name.StartsWith("news-", StringComparison.OrdinalIgnoreCase)
                        || f.Name.StartsWith("quotes-", StringComparison.OrdinalIgnoreCase)
                        || f.Name.Equals("weather.json", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(f => f.LastWriteTimeUtc).ToList();
                var bytes = files.Sum(f => f.Length);
                var count = files.Count;
                foreach (var file in files)
                {
                    if (count <= MaxCacheFiles && bytes <= MaxCacheBytes) break;
                    if (string.Equals(file.FullName, preserve, StringComparison.OrdinalIgnoreCase)) continue;
                    try
                    {
                        var length = file.Length;
                        file.Delete();
                        WrittenHashes.Remove(file.FullName);
                        count--; bytes -= length;
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    /// <summary>The stored copy, or null when there is none or it cannot be read.</summary>
    public static Cached<T>? Load<T>(string name)
    {
        try
        {
            var path = PathOf(name);
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > MaxEntryBytes) return null;
            var result = JsonSerializer.Deserialize<Cached<T>>(File.ReadAllText(path));
            if (result is not null)
            {
                // Seed the fingerprint from the persisted cache at startup: the first unchanged
                // fetch after restarting the app will not rewrite the SSD.
                var payload = JsonSerializer.Serialize(result.Data);
                var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(payload)));
                lock (WriteGate)
                {
                    if (WrittenHashes.Count >= MaxCacheFiles * 2) WrittenHashes.Clear();
                    WrittenHashes[path] = hash;
                }
            }
            return result;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return null;
        }
    }
}
