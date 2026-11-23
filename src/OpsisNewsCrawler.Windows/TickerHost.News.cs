using OpsisNewsCrawler.Core;
using System.Diagnostics;

namespace OpsisNewsCrawler.Windows;

/// <summary>P0 module split from the ticker host; no change to the public host lifecycle or rendering.</summary>
public sealed partial class TickerHost
{
    private async Task RefreshExtraNewsAsync()
    {
        if (disposed || refreshingExtra) return;
        var wanted = ExtraNewsCategories();
        if (wanted.Count == 0) return;
        refreshingExtra = true;
        try
        {
            foreach (var id in wanted)
            {
                if (disposed || cts.IsCancellationRequested) return;
                var items = await extraNewsProvider.FetchAsync(Config.GetFeeds(id), Config.MaxItemsPerFeed, lifetimeToken, Config.NewsMaxAgeHours, Config.NewsSortNewestFirst);
                var failedFeeds = extraNewsProvider.LastFailedFeeds.ToList();
                var category = id;
                void Apply()
                {
                    if (disposed) return;
                    if (items.Count > 0)
                    {
                        extraNews[category] = items.ToList();
                        extraNewsAt[category] = DateTimeOffset.Now;
                        extraNewsFailed.Remove(category);
                        DataCache.Save("news-" + category, extraNews[category]);
                    }
                    else extraNewsFailed[category] = failedFeeds;
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
        if (disposed) return;
        if (!NeedsNews) return;
        if (refreshing)
        {
            // Remember a forced request (e.g. mode change) and run it as soon as the current one ends.
            if (force) pendingForced = true;
            return;
        }
        if (!force && State.LastNewsRefresh is { } last && DateTimeOffset.UtcNow - last < MinRefreshGap) return;

        refreshing = true;
        try
        {
            var category = Config.ActiveCategory;
            var items = await news.FetchAsync(Config.GetFeeds(category), Config.MaxItemsPerFeed, lifetimeToken, Config.NewsMaxAgeHours, Config.NewsSortNewestFirst);
            var failedFeeds = news.LastFailedFeeds.ToList();
            if (disposed || !string.Equals(category, Config.ActiveCategory, StringComparison.OrdinalIgnoreCase)) return; // the category changed meanwhile; a new refresh is running

            void Apply()
            {
                if (disposed) return;
                State.MarkNewsRefresh();
                if (items.Count > 0)
                {
                    hasRealItems = true;
                    allItems = items.ToList();
                    newsDataAt = DateTimeOffset.Now;
                    newsFailedFeeds = new();
                    DataCache.Save("news-" + category, allItems);
                    DetectBreaking(allItems);
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
            if (pendingForced && !disposed)
            {
                pendingForced = false;
                _ = RefreshAsync(force: true);
            }
        }
    }
}
