using System.Globalization;

namespace OpsisNewsCrawler.Core;

/// <summary>One dated closing price from data the program already holds.</summary>
public readonly record struct DatedClose(DateOnly Day, decimal Close);

/// <summary>Annualised volatility of daily returns over a stated number of samples (N26).</summary>
public sealed record Volatility(double AnnualisedPercent, int Returns, DateOnly From, DateOnly To);

/// <summary>The largest fall from a peak to a later trough inside the available series (N27).</summary>
public sealed record Drawdown(double Percent, DateOnly Peak, DateOnly Trough);

/// <summary>Two price series over their common days (N24): price return only, no dividends.</summary>
public sealed record Comparison(double SeriesPercent, double BenchmarkPercent, int CommonDays, DateOnly From, DateOnly To);

/// <summary>A holding's weight inside its own currency (N25); different currencies are never added together.</summary>
public sealed record AllocationRow(string Name, string Currency, decimal Value, double PercentOfCurrency);

public sealed record AllocationResult(IReadOnlyList<AllocationRow> Rows, int Valued, int Unvalued);

/// <summary>A portfolio value at a moment, and money moved in (+) or out (-) at that moment, before the value was taken.</summary>
public sealed record ValuationPoint(DateOnly Day, decimal Value, decimal ExternalFlow = 0);

/// <summary>
/// Financial measures (N21, N24-N28) from existing prices and ledger data. Every measure returns null when the data cannot support it: nothing is
/// invented, interpolated or converted between currencies, and none of this is investment advice.
/// </summary>
public static class MarketMetrics
{
    public const int MinSamples = 20;

    /// <summary>The closes of a candle list, oldest first; rows with a bad day or a non-positive close are left out, the last row of a day wins.</summary>
    public static IReadOnlyList<DatedClose> Series(IEnumerable<Candle> candles)
    {
        ArgumentNullException.ThrowIfNull(candles);
        var byDay = new SortedDictionary<DateOnly, decimal>();
        foreach (var c in candles)
            if (c.Close > 0 && DateOnly.TryParseExact(c.Day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)) byDay[day] = c.Close;
        return byDay.Select(p => new DatedClose(p.Key, p.Value)).ToArray();
    }

    public static Volatility? AnnualisedVolatility(IReadOnlyList<DatedClose> series, int minSamples = MinSamples)
    {
        ArgumentNullException.ThrowIfNull(series);
        if (series.Count < Math.Max(2, minSamples) + 1) return null;
        var returns = new List<double>(series.Count - 1);
        for (var i = 1; i < series.Count; i++) returns.Add(Math.Log((double)(series[i].Close / series[i - 1].Close)));
        var mean = returns.Average();
        var variance = returns.Sum(r => (r - mean) * (r - mean)) / (returns.Count - 1);
        return new Volatility(Math.Round(Math.Sqrt(variance) * Math.Sqrt(252) * 100, 2), returns.Count, series[0].Day, series[^1].Day);
    }

    public static Drawdown? MaxDrawdown(IReadOnlyList<DatedClose> series)
    {
        ArgumentNullException.ThrowIfNull(series);
        if (series.Count < 2) return null;
        var peak = series[0];
        double worst = 0;
        DateOnly peakDay = series[0].Day, troughDay = series[0].Day;
        foreach (var point in series)
        {
            if (point.Close > peak.Close) { peak = point; continue; }
            var fall = (double)((peak.Close - point.Close) / peak.Close);
            if (fall > worst) { worst = fall; peakDay = peak.Day; troughDay = point.Day; }
        }
        return new Drawdown(Math.Round(worst * 100, 2), peakDay, troughDay);
    }

    public static Comparison? Compare(IReadOnlyList<DatedClose> series, IReadOnlyList<DatedClose> benchmark)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(benchmark);
        var other = benchmark.ToDictionary(b => b.Day, b => b.Close);
        var common = series.Where(s => other.ContainsKey(s.Day)).OrderBy(s => s.Day).ToList();
        if (common.Count < 2) return null;
        var first = common[0];
        var last = common[^1];
        double Percent(decimal from, decimal to) => Math.Round((double)((to - from) / from) * 100, 2);
        return new Comparison(Percent(first.Close, last.Close), Percent(other[first.Day], other[last.Day]), common.Count, first.Day, last.Day);
    }

    public static AllocationResult Allocation(IEnumerable<(string Name, string Currency, decimal? Value)> holdings)
    {
        ArgumentNullException.ThrowIfNull(holdings);
        var all = holdings.ToList();
        var valued = all.Where(h => h.Value is > 0 && !string.IsNullOrWhiteSpace(h.Currency)).ToList();
        var totals = valued.GroupBy(h => h.Currency.Trim().ToUpperInvariant()).ToDictionary(g => g.Key, g => g.Sum(x => x.Value!.Value));
        var rows = valued.Select(h =>
        {
            var currency = h.Currency.Trim().ToUpperInvariant();
            return new AllocationRow(h.Name, currency, h.Value!.Value, Math.Round((double)(h.Value.Value / totals[currency]) * 100, 2));
        }).OrderBy(r => r.Currency).ThenByDescending(r => r.Value).ToArray();
        return new AllocationResult(rows, valued.Count, all.Count - valued.Count);
    }

    /// <summary>
    /// The valuation points of one instrument's ledger: the value after each trade day (units held x that day's close), the money that moved on that day
    /// (a buy is money in; a sale or dividend money out) and the latest close. Null (unavailable) when a trade day has no stored price, the ledger is
    /// inconsistent, or it contains a split (the stored prices are not adjusted for it).
    /// </summary>
    public static IReadOnlyList<ValuationPoint>? LedgerValuations(IEnumerable<PortfolioTransaction> transactions, IReadOnlyList<DatedClose> series)
    {
        ArgumentNullException.ThrowIfNull(transactions);
        ArgumentNullException.ThrowIfNull(series);
        var trades = transactions.OrderBy(t => t.When).ToList();
        if (trades.Count == 0 || series.Count == 0 || trades.Any(t => t.Action == PortfolioAction.Split)) return null;
        var close = series.ToDictionary(c => c.Day, c => c.Close);
        var points = new List<ValuationPoint>();
        decimal units = 0;
        try
        {
            foreach (var day in trades.GroupBy(t => DateOnly.FromDateTime(t.When.LocalDateTime)))
            {
                if (!close.TryGetValue(day.Key, out var price)) return null;
                decimal flow = 0;
                foreach (var t in day)
                {
                    if (t.Action == PortfolioAction.Buy) { units += t.Units; flow += t.Units * t.UnitPrice + t.Fees; }
                    else if (t.Action == PortfolioAction.Sell) { units -= t.Units; flow -= t.Units * t.UnitPrice - t.Fees; }
                    else flow -= t.Units * t.UnitPrice - t.Fees;
                }
                if (units < 0) return null;
                points.Add(new ValuationPoint(day.Key, units * price, flow));
            }
            var last = series[^1];
            if (last.Day > points[^1].Day) points.Add(new ValuationPoint(last.Day, units * last.Close));
        }
        catch (OverflowException) { return null; }
        return points;
    }

    /// <summary>
    /// Time-weighted return (N28): the product of the sub-period returns between consecutive valuations, where each point carries the flow made at that
    /// moment (the value includes it). Null when there are fewer than two points, a start value is not positive, or the points are not in day order:
    /// a flow without a valuation cannot be handled, so missing data means no number.
    /// </summary>
    public static double? TimeWeightedReturnPercent(IReadOnlyList<ValuationPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count < 2) return null;
        double growth = 1;
        for (var i = 1; i < points.Count; i++)
        {
            var previous = points[i - 1];
            var now = points[i];
            if (now.Day <= previous.Day || previous.Value <= 0) return null;
            var end = now.Value - now.ExternalFlow;   // the value before the money moved at this point
            if (end < 0) return null;
            growth *= (double)(end / previous.Value);
        }
        return Math.Round((growth - 1) * 100, 2);
    }
}
