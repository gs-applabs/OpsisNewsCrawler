namespace OpsisNewsCrawler.Core;

/// <summary>Writes small settings/cache files so that a crash or power loss never leaves a half-written file behind.</summary>
public static class SafeFile
{
    private static long writeCount, bytesWritten;
    /// <summary>Logical file payload successfully committed via SafeFile; not physical SSD NAND writes.</summary>
    public static (long Writes, long Bytes) Diagnostics =>
        (Interlocked.Read(ref writeCount), Interlocked.Read(ref bytesWritten));

    public static void WriteAllText(string path, string text)
    {
        lock (WriteGate)
        {
            var temp = path + ".tmp";
            try
            {
                File.WriteAllText(temp, text);
                var bytes = new FileInfo(temp).Length;
                File.Move(temp, path, overwrite: true);
                Interlocked.Increment(ref writeCount);
                Interlocked.Add(ref bytesWritten, bytes);
            }
            finally
            {
                try { File.Delete(temp); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
        }
    }

    /// <summary>
    /// Avoids repetitive SSD writes for unchanged quote history files. The first call compares
    /// with the persisted copy so even an app restart does not rewrite identical history.
    /// </summary>
    public static bool WriteAllTextIfChanged(string path, string text)
    {
        lock (WriteGate)
        {
            if (File.Exists(path) && string.Equals(File.ReadAllText(path), text, StringComparison.Ordinal))
                return false;
            WriteAllText(path, text);
            return true;
        }
    }

    private static readonly object WriteGate = new();

    /// <summary>Moves an unreadable file aside (once) so the user's data is not overwritten by defaults.</summary>
    public static void KeepAsBad(string path)
    {
        try
        {
            File.Move(path, path + ".bad", overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
