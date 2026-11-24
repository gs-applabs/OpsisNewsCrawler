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
    /// <summary>Optional re-arm margin in price units or percentage points.</summary>
    public decimal RearmMargin { get; set; }
    /// <summary>Optional maximum observation age in minutes; zero keeps legacy behavior.</summary>
    public int MaxAgeMinutes { get; set; }

    /// <summary>True while the limit is passed and the alert was already shown; re-arms when the price comes back. Not saved.</summary>
    [JsonIgnore]
    public bool Fired { get; set; }

    public PriceAlert Clone() => new()
    {
        Source = Source, Code = Code, Market = Market, Label = Label, Above = Above, Threshold = Threshold,
        Enabled = Enabled, Fired = Fired, Metric = Metric, RearmMargin = RearmMargin, MaxAgeMinutes = MaxAgeMinutes
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
        var now = DateTimeOffset.UtcNow;
        foreach (var alert in alerts)
        {
            if (!alert.Enabled || (alert.Metric == "changePercent" ? alert.Threshold is < -100 or > 100 : alert.Threshold <= 0)) continue;
            MarketQuote? quote = null;
            foreach (var (instrument, candidate) in prices)
                if (alert.Matches(instrument)) { quote = candidate; break; }
            if (quote is null) continue;
            if (alert.MaxAgeMinutes > 0 && (quote.AsOf > now.AddMinutes(5) ||
                now - quote.AsOf > TimeSpan.FromMinutes(Math.Clamp(alert.MaxAgeMinutes, 1, 10080)))) continue;

            var observed = alert.Metric == "changePercent" ? quote.ChangePercent : quote.Price;
            if (observed is null) continue; // missing data must not re-arm an already fired rule
            var passed = alert.Above ? observed.Value >= alert.Threshold : observed.Value <= alert.Threshold;
            if (passed && !alert.Fired)
            {
                alert.Fired = true;
                hits.Add((alert, quote));
            }
            else if (!passed)
            {
                var margin = Math.Clamp(alert.RearmMargin, 0m, 1_000_000m);
                // Compare distances without adding a margin to potentially extreme thresholds.
                if (margin == 0 || (alert.Above ? (double)alert.Threshold - (double)observed.Value :
                    (double)observed.Value - (double)alert.Threshold) >= (double)margin) alert.Fired = false;
            }
        }
        return hits;
    }
}
