using OpsisNewsCrawler.Core;
using OpsisNewsCrawler.Plugin.Abstractions;
using System.Diagnostics;

namespace OpsisNewsCrawler.Windows;

/// <summary>P0 module split from the ticker host; no change to the public host lifecycle or rendering.</summary>
public sealed partial class TickerHost
{
    private CancellationTokenSource? historyBackfill;

    private void CancelHistoryBackfill() => historyBackfill?.Cancel();

    /// <summary>
    /// When the user switched it on (Sources &gt; Markets flow): fills the stored price history from Yahoo Finance, a few instruments per run,
    /// one try per instrument and day. Off by default.
    /// </summary>
    public async void StartHistoryBackfill()
    {
        if (!Config.HistoryFromYahoo || !NeedsStocks || historyBackfill is not null || disposed || !started ||
            !activeServices.TryGetValue("quotes", out var demand)) return;
        using var lease = CancellationTokenSource.CreateLinkedTokenSource(demand.Token);
        historyBackfill = lease;
        var epoch = startEpoch;
        var token = lease.Token;
        try
        {
            var list = Config.Watchlist.Where(w => w.Enabled).Concat(PortfolioInstruments()).ToList();
            var ui = SynchronizationContext.Current;
            var n = await YahooHistory.BackfillAsync(quotes.Store, list, 40, token, () => ui?.Post(_ => { if (!disposed && started && epoch == startEpoch && !token.IsCancellationRequested && Config.HistoryFromYahoo) QuotesUpdated?.Invoke(); }, null));
            if (n > 0 && !disposed && started && epoch == startEpoch && !token.IsCancellationRequested && Config.HistoryFromYahoo)
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
            historyBackfill = null;
        }
    }

    private IEnumerable<QuoteInstrument> PortfolioInstruments() => Config.Portfolio.Select(p => p.ToInstrument());

    private async Task RefreshQuotesAsync(bool force = false)
    {
        if (disposed || !started || !NeedsStocks || refreshingQuotes) return;
        refreshingQuotes = true;
        var epoch = startEpoch;
        var token = DemandToken("quotes");
        var ui = SynchronizationContext.Current;
        var uiThread = Environment.CurrentManagedThreadId;
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
                        PublishSuiteEvent(new SuiteEvent(SuiteEventKind.Market, "crawler", item.Source,
                            item.Title, item.PublishedAt, item.Url, item.Summary));
                    PushAll();
                }

                if (Environment.CurrentManagedThreadId == uiThread) Apply();
                else if (Window.IsHandleCreated) Window.BeginInvoke(new Action(Apply));
                else ui?.Post(_ => Apply(), null);
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
