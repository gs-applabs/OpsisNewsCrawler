using System.Text.Json.Serialization;

namespace OpsisNewsCrawler.Core;

/// <summary>"Tell me when this price goes above / below a limit." Shown on the bar like a breaking headline.</summary>
public sealed class PriceAlert
{
    public QuoteSource Source { get; set; }
    /// <summary>Instrument code (ISIN, symbol or series id), as in the watch list.</summary>
    public string Code { get; set; } = "";
    public string Market { get; set; } = "";
    /// <summary>Text shown in the settings list and in the alert (usually the symbol).</summary>
    public string Label { get; set; } = "";
    public bool Above { get; set; } = true;
    /// <summary>price (legacy) or changePercent (day-over-day, from source when available).</summary>
    public string Metric { get; set; } = "price";
    public decimal Threshold { get; set; }
    public bool Enabled { get; set; } = true;

    /// <summary>True while the limit is passed and the alert was already shown; re-arms when the price comes back. Not saved.</summary>
    [JsonIgnore]
    public bool Fired { get; set; }

    public PriceAlert Clone() => new()
    {
        Source = Source, Code = Code, Market = Market, Label = Label, Above = Above, Threshold = Threshold,
        Enabled = Enabled, Fired = Fired, Metric = Metric
    };

    public bool Matches(QuoteInstrument instrument) =>
        Source == instrument.Source
        && string.Equals(Code, instrument.Code, StringComparison.OrdinalIgnoreCase)
        && string.Equals(Market ?? "", instrument.Market ?? "", StringComparison.OrdinalIgnoreCase);
}

public static class PriceAlertCheck
{
    /// <summary>
    /// The alerts whose limit has just been passed (each one is returned once, until the price goes back).
    /// Updates <see cref="PriceAlert.Fired"/>.
    /// </summary>
    public static List<(PriceAlert Alert, MarketQuote Quote)> Evaluate(
        IEnumerable<PriceAlert> alerts, IReadOnlyList<(QuoteInstrument Instrument, MarketQuote Quote)> prices)
    {
        var hits = new List<(PriceAlert, MarketQuote)>();
        foreach (var alert in alerts)
        {
            if (!alert.Enabled || (alert.Metric == "changePercent" ? alert.Threshold is < -100 or > 100 : alert.Threshold <= 0)) continue;
            MarketQuote? quote = null;
            foreach (var (instrument, candidate) in prices)
                if (alert.Matches(instrument)) { quote = candidate; break; }
            if (quote is null) continue;

            var observed = alert.Metric == "changePercent" ? quote.ChangePercent : quote.Price;
            if (observed is null) { alert.Fired = false; continue; }
            var passed = alert.Above ? observed.Value >= alert.Threshold : observed.Value <= alert.Threshold;
            if (passed && !alert.Fired)
            {
                alert.Fired = true;
                hits.Add((alert, quote));
            }
            else if (!passed) alert.Fired = false;
        }
        return hits;
    }
}
