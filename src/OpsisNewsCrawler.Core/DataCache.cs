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
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(payload)));
            lock (WriteGate)
            {
                if (WrittenHashes.TryGetValue(path, out var previous) && previous == hash)
                {
                    Interlocked.Increment(ref skippedWrites);
                    return;
                }
                Directory.CreateDirectory(Folder);
                SafeFile.WriteAllText(path, JsonSerializer.Serialize(new Cached<T>(DateTimeOffset.UtcNow, data)));
                WrittenHashes[path] = hash; // on success only; failed writes may be retried
                Interlocked.Increment(ref completedWrites);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // A cache that cannot be written is not a problem for the bar.
        }
    }

    /// <summary>The stored copy, or null when there is none or it cannot be read.</summary>
    public static Cached<T>? Load<T>(string name)
    {
        try
        {
            var path = PathOf(name);
            return File.Exists(path) ? JsonSerializer.Deserialize<Cached<T>>(File.ReadAllText(path)) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return null;
        }
    }
}
