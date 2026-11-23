using OpsisNewsCrawler.Core;
using System.Diagnostics;

namespace OpsisNewsCrawler.Windows;

/// <summary>P0 module split from the ticker host; no change to the public host lifecycle or rendering.</summary>
public sealed partial class TickerHost
{
    private readonly Dictionary<string, (DateTime Day, DateTime At, string Signature, IReadOnlyList<NewsItem> Items)> pluginFeedCache = new(StringComparer.OrdinalIgnoreCase);
    private bool pluginFeedCacheWired;

    /// <summary>
    /// The lines of a feed that a plugin registered. A rich feed (details per line) wins over a text feed of the same key. The result is kept for the day and for one minute at most
    /// (the plugin fills its feed in the background; the registry's Changed event empties the cache at once), so the strip does not ask the plugin at every refresh. A failing plugin gives an
    /// empty bar and a log line, never an exception.
    /// </summary>
    private IReadOnlyList<NewsItem> PluginFeedForDisplay(string? key)
    {
        var rich = OpsisNewsCrawler.Plugin.Abstractions.PluginFeeds.FindRich(key);
        var text = rich is null ? OpsisNewsCrawler.Plugin.Abstractions.PluginFeeds.Find(key) : null;
        var feedKey = rich?.Key ?? text?.Key;
        if (feedKey is null || Config.DisabledPluginFeeds.Contains(feedKey, StringComparer.OrdinalIgnoreCase)) return Array.Empty<NewsItem>();
        if (!pluginFeedCacheWired) { pluginFeedCacheWired = true; OpsisNewsCrawler.Plugin.Abstractions.PluginFeeds.Changed += () => { lock (pluginFeedCache) pluginFeedCache.Clear(); }; }

        var options = PluginFeedOptions.From(Config, Loc.Language);
        var signature = $"{options.MaxLines}|{options.Order}|{options.Country}|{options.Language}|{(rich is null ? "t" : "r")}";
        lock (pluginFeedCache)
            if (pluginFeedCache.TryGetValue(feedKey, out var hit) && hit.Day == DateTime.Today && hit.Signature == signature && DateTime.UtcNow - hit.At < TimeSpan.FromMinutes(1)) return hit.Items;
        try
        {
            var now = DateTimeOffset.Now;
            IReadOnlyList<NewsItem> items = rich is not null
                ? PluginFeedMapper.FromRich(rich.GetItems(), Loc.Pick(rich.TitleEl, rich.TitleEn), now, options)
                : PluginFeedMapper.FromText(text!.GetItems(), Loc.Pick(text.TitleEl, text.TitleEn), now, options);
            lock (pluginFeedCache) pluginFeedCache[feedKey] = (DateTime.Today, DateTime.UtcNow, signature, items);
            return items;
        }
        catch (Exception ex)
        {
            Log.Write($"Plugin feed '{key}' failed: {ex.Message}");
            return Array.Empty<NewsItem>();
        }
    }
}
