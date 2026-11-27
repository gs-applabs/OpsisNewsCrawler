using OpsisNewsCrawler.Core;
using OpsisNewsCrawler.Plugin.Abstractions;
using System.Diagnostics;

namespace OpsisNewsCrawler.Windows;

/// <summary>P0 module split from the ticker host; no change to the public host lifecycle or rendering.</summary>
public sealed partial class TickerHost
{
    private async Task RefreshExtraNewsAsync()
    {
        if (disposed || !started || !NeedsNews || refreshingExtra) return;
        PruneExtraCategories();
        var wanted = ExtraNewsCategories();
        if (wanted.Count == 0) return;
        refreshingExtra = true;
        var epoch = startEpoch;
        var token = DemandToken("news");
        try
        {
            foreach (var id in wanted)
            {
                if (disposed || !started || epoch != startEpoch || token.IsCancellationRequested) return;
                var items = await news.FetchAsync(Config.GetFeeds(id), Config.MaxItemsPerFeed, token, Config.NewsMaxAgeHours, Config.NewsSortNewestFirst, TimeSpan.FromSeconds(30));
                var failedFeeds = news.LastFailedFeeds.ToList();
                var category = id;
                void Apply()
                {
                    if (disposed || !started || token.IsCancellationRequested || epoch != startEpoch || !ExtraNewsCategories().Contains(category, StringComparer.OrdinalIgnoreCase)) return;
                    if (items.Count > 0)
                    {
                        extraNews[category] = items.ToList();
                        extraNewsAt[category] = DateTimeOffset.Now;
                        extraNewsFailed.Remove(category);
                        DataCache.Save("news-" + category, extraNews[category]);
                    }
                    else extraNewsFailed[category] = failedFeeds;
                    if (newsWidgetUsers > 0 && string.Equals(
                        WidgetStudio.Resolve(Config.WidgetProfiles, "news").NewsCategoryId,
                        category, StringComparison.OrdinalIgnoreCase))
                    {
                        // A UI-only invalidation must not create a synthetic
                        // news item on the cross-app SuiteEventHub.
                        NewsWidgetUpdated?.Invoke();
                    }
                    PushAll();
                }
                if (Window.IsHandleCreated && Window.InvokeRequired) Window.Invoke(new Action(Apply)); else Apply();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Log.Write($"extra news failed: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            refreshingExtra = false;
        }
    }

    private async Task RefreshAsync(bool force)
    {
        if (disposed || !started) return;
        if (!NeedsNews) return;
        if (!NeedsActiveNews) { await RefreshExtraNewsAsync(); return; }
        if (refreshing)
        {
            // Remember a forced request (e.g. mode change) and run it as soon as the current one ends.
            if (force) pendingForced = true;
            return;
        }
        if (!force && State.LastNewsRefresh is { } last && DateTimeOffset.UtcNow - last < MinRefreshGap) return;

        refreshing = true;
        var epoch = startEpoch;
        var token = DemandToken("news");
        try
        {
            var category = Config.ActiveCategory;
            var items = await news.FetchAsync(Config.GetFeeds(category), Config.MaxItemsPerFeed, token, Config.NewsMaxAgeHours, Config.NewsSortNewestFirst, TimeSpan.FromSeconds(30));
            var failedFeeds = news.LastFailedFeeds.ToList();
            if (disposed || !started || token.IsCancellationRequested || epoch != startEpoch || !NeedsActiveNews || !string.Equals(category, Config.ActiveCategory, StringComparison.OrdinalIgnoreCase)) return; // the category changed meanwhile; a new refresh is running

            void Apply()
            {
                if (disposed || !started || token.IsCancellationRequested || epoch != startEpoch || !NeedsActiveNews) return;
                State.MarkNewsRefresh();
                if (items.Count > 0)
                {
                    hasRealItems = true;
                    allItems = items.ToList();
                    newsDataAt = DateTimeOffset.Now;
                    newsFailedFeeds = new();
                    DataCache.Save("news-" + category, allItems);
                    DetectBreaking(allItems);
                    if (Config.ArchiveNews) Archive.Observe(allItems, DateTimeOffset.UtcNow);
                    foreach (var item in allItems.Where(i => i.Kind == NewsItemKind.News).Take(20))
                        PublishSuiteEvent(new SuiteEvent(SuiteEventKind.News, "crawler", item.Url,
                            item.Title, item.PublishedAt, item.Url, item.Summary));
                }
                else
                {
                    // Nothing came back: keep (and say that we keep) what we showed or stored before.
                    newsFailedFeeds = failedFeeds;
                }
                PushAll();
            }

            if (Window.IsHandleCreated && Window.InvokeRequired) Window.Invoke(new Action(Apply));
            else Apply();
            await RefreshExtraNewsAsync();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Opsis News Crawler: refresh failed: {ex}");
        }
        finally
        {
            refreshing = false;
            if (pendingForced && !disposed && started)
            {
                pendingForced = false;
                RequestRefresh("news", () => RefreshAsync(force: true), latest: true);
            }
        }
    }
}
