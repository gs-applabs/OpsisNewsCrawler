namespace OpsisNewsCrawler.Core;

/// <summary>Reuse the existing explicit data-folder override used by QuoteStore and CI.
/// The default production location is unchanged; settings retain their explicit path.</summary>
public static class CrawlerDataPaths
{
    public static string Root => Resolve(Environment.GetEnvironmentVariable("OPSISNEWSCRAWLER_DATA"));
    internal static string Resolve(string? configured) => string.IsNullOrWhiteSpace(configured)
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpsisNewsCrawler")
        : configured;
}
