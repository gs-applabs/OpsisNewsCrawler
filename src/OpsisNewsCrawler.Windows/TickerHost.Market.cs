using OpsisNewsCrawler.Core;
using OpsisNewsCrawler.Plugin.Abstractions;
using System.Diagnostics;

namespace OpsisNewsCrawler.Windows;

/// <summary>P0 module split from the ticker host; no change to the public host lifecycle or rendering.</summary>
public sealed partial class TickerHost
{
    private bool backfilling;

    /// <summary>
    /// When the user switched it on (Sources &gt; Markets flow): fills the stored price history from Yahoo Finance, a few instruments per run,
    /// one try per instrument and day. Off by default.
    /// </summary>
    public async void StartHistoryBackfill()
    {
        if (!Config.HistoryFromYahoo || !NeedsStocks || backfilling || disposed || !started) return;
        backfilling = true;
        var epoch = startEpoch;
        var token = DemandToken("quotes");
        try
        {
            var list = Config.Watchlist.Where(w => w.Enabled).Concat(PortfolioInstruments()).ToList();
            var ui = SynchronizationContext.Current;
            var n = await YahooHistory.BackfillAsync(quotes.Store, list, 40, token, () => ui?.Post(_ => { if (!disposed && started && epoch == startEpoch) QuotesUpdated?.Invoke(); }, null));
            if (n > 0 && !disposed && started && epoch == startEpoch)
            {
                Log.Write($"Yahoo history added for {n} instruments.");
                QuotesUpdated?.Invoke();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Log.Write($"history backfill failed: {ex.GetType().Name}: {ex.Message}");   // async void: nothing may escape
        }
        finally
        {
            backfilling = false;
        }
    }

    private IEnumerable<QuoteInstrument> PortfolioInstruments() => Config.Portfolio.Select(p => p.ToInstrument());

    private async Task RefreshQuotesAsync(bool force = false)
    {
        if (disposed || !started || !NeedsStocks || refreshingQuotes) return;
        refreshingQuotes = true;
        var epoch = startEpoch;
        var token = DemandToken("quotes");
        try
        {
            void Show(IReadOnlyList<NewsItem> list)
            {
                void Apply()
                {
                    if (disposed || !started || token.IsCancellationRequested || epoch != startEpoch || !NeedsStocks) return;
                    hasQuotes = true;
                    if (list.Count > 0 || stockItems.Count == 0) stockItems = list; // nothing came back: keep what is shown
                    foreach (var item in stockItems.Where(x => x.Kind == NewsItemKind.Quote).Take(40))
                        SuiteEventHub.Publish(new SuiteEvent(SuiteEventKind.Market, "crawler", item.Source,
                            item.Title, item.PublishedAt, item.Url, item.Summary));
                    PushAll();
                }

                if (!Window.IsHandleCreated) return;
                if (Window.InvokeRequired) Window.BeginInvoke(new Action(Apply));
                else Apply();
            }

            // Each source shows up as soon as it has answered; a slow one cannot keep the others waiting.
            var items = await quotes.FetchItemsAsync(Config.Watchlist, Loc.IsGreek, token, Show, PortfolioInstruments());
            if (disposed || !started || token.IsCancellationRequested || epoch != startEpoch || !NeedsStocks) return;
            quotesFailed = quotes.LastFailed.Count > 0 || items.Count == 0 && Config.Watchlist.Any(w => w.Enabled);
            Show(items);
            QuotesUpdated?.Invoke();
            StartHistoryBackfill();
            CheckPriceAlerts();
            if (quotesFailed) ScheduleQuoteRetry();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Opsis News Crawler: quotes failed: {ex}");
            Log.Write($"quotes failed: {ex.GetType().Name}: {ex.Message}");
            if (disposed || !started || token.IsCancellationRequested || epoch != startEpoch || !NeedsStocks) return;
            hasQuotes = true; // the bar says "unavailable" instead of "loading" for ever
            quotesFailed = true;
            ScheduleQuoteRetry();
        }
        finally
        {
            refreshingQuotes = false;
        }
    }
}
