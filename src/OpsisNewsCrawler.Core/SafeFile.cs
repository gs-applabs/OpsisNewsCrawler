namespace OpsisNewsCrawler.Core;

/// <summary>Writes small settings/cache files so that a crash or power loss never leaves a half-written file behind.</summary>
internal static class SafeFile
{
    public static void WriteAllText(string path, string text)
    {
        var temp = path + ".tmp";
        File.WriteAllText(temp, text);
        File.Move(temp, path, overwrite: true);
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
