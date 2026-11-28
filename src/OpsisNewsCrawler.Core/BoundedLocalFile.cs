namespace OpsisNewsCrawler.Core;

/// <summary>Bound bytes actually read, not only a file length observed before opening.
/// A concurrently enlarged file cannot cause an unbounded allocation.</summary>
public static class BoundedLocalFile
{
    public static byte[]? Read(string path, int maxBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        return Read(stream, maxBytes);
    }

    internal static byte[]? Read(Stream stream, int maxBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
        if (stream.CanSeek && stream.Length - stream.Position > maxBytes) return null;
        using var output = new MemoryStream();
        var buffer = new byte[Math.Min(8192, maxBytes)];
        while (output.Length < maxBytes)
        {
            var count = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, maxBytes - output.Length));
            if (count == 0) return output.ToArray();
            output.Write(buffer, 0, count);
        }
        return stream.ReadByte() == -1 ? output.ToArray() : null;
    }
}
