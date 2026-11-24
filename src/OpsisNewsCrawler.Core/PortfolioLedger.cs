namespace OpsisNewsCrawler.Core;

public enum PortfolioAction { Buy, Sell, Dividend, Split }

/// <summary>All monetary fields are in the instrument's original currency; Split uses Units as the multiplier.</summary>
public sealed record PortfolioTransaction(string InstrumentKey, DateTimeOffset When, PortfolioAction Action,
    decimal Units, decimal UnitPrice, decimal Fees = 0, string Currency = "EUR");

public sealed record LedgerPosition(decimal Units, decimal CostBasis, decimal RealizedPnl, decimal Dividends, decimal Fees)
{
    public decimal? AverageUnitCost => Units > 0 ? CostBasis / Units : null;
    public decimal NetRealizedIncome => RealizedPnl + Dividends;
}

public static class PortfolioLedger
{
    // QuoteService uses the same key layout as PortfolioPosition.Key.
    // Only explicit configured price units are accepted; no currency conversion is inferred.
    public static bool MatchesQuoteCurrency(QuoteInstrument instrument, string currency)
    {
        var unit=(instrument.Unit??"").Trim();
        var code=NormalizeCurrency(currency);
        return string.Equals(unit,code,StringComparison.OrdinalIgnoreCase)
            || (unit=="€" && code=="EUR" && instrument.Source==QuoteSource.Athens);
    }

    public static (decimal Value, decimal Unrealized, decimal? Percent)? ValueAt(
        LedgerPosition position, MarketQuote? quote)
    {
        if(quote is null || quote.Price<=0 || position.Units<=0) return null;
        var value=position.Units*quote.Price;
        var pnl=value-position.CostBasis;
        return (value,pnl,position.CostBasis>0?Math.Round(pnl/position.CostBasis*100,2):null);
    }

    public static string NormalizeCurrency(string? currency)
    {
        var code=(currency??"").Trim().ToUpperInvariant();
        if(code.Length!=3 || !code.All(c=>c is >= 'A' and <= 'Z'))
            throw new ArgumentException("Currency must be a three-letter code.",nameof(currency));
        return code;
    }

    /// <summary>Weighted average cost including buy fees; no tax treatment or FX assumptions.</summary>
    public static LedgerPosition Calculate(IEnumerable<PortfolioTransaction> transactions)
    {
        decimal units=0, cost=0, realized=0, dividends=0, fees=0;
        string? currency=null, instrument=null;
        foreach (var t in transactions.OrderBy(x => x.When))
        {
            var code=NormalizeCurrency(t.Currency);
            if(string.IsNullOrWhiteSpace(t.InstrumentKey)) throw new ArgumentException("Instrument key required.");
            currency ??= code;
            instrument ??= t.InstrumentKey.Trim();
            if(currency!=code || !string.Equals(instrument,t.InstrumentKey.Trim(),StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("A ledger position must contain one instrument and currency.");
            if(!Enum.IsDefined(t.Action)) throw new ArgumentException("Unknown portfolio action.");
            if (t.Fees < 0 || t.Units <= 0 || t.UnitPrice < 0)
                throw new ArgumentOutOfRangeException(nameof(transactions), "Invalid transaction values");
            fees += t.Fees;
            switch (t.Action)
            {
                case PortfolioAction.Buy:
                    cost += t.Units * t.UnitPrice + t.Fees;
                    units += t.Units;
                    break;
                case PortfolioAction.Sell:
                    if (t.Units > units) throw new InvalidOperationException("Cannot sell more units than held.");
                    var released = units == 0 ? 0 : cost * t.Units / units;
                    realized += t.Units * t.UnitPrice - t.Fees - released;
                    cost -= released;
                    units -= t.Units;
                    if (units == 0) cost = 0;
                    break;
                case PortfolioAction.Dividend:
                    dividends += t.Units * t.UnitPrice - t.Fees;
                    break;
                case PortfolioAction.Split:
                    if (t.UnitPrice != 0 || t.Fees != 0) throw new ArgumentException("Split may not have a price or fee.");
                    if (units <= 0) throw new InvalidOperationException("Cannot split an empty position.");
                    units *= t.Units;
                    break;
            }
        }
        return new LedgerPosition(units, cost, realized, dividends, fees);
    }
}
