namespace OpsisNewsCrawler.Core;

public enum PortfolioAction { Buy, Sell, Dividend, Split }

/// <summary>All monetary fields are in the instrument's original currency; Split uses Units as the multiplier.</summary>
public sealed record PortfolioTransaction(string InstrumentKey, DateTimeOffset When, PortfolioAction Action,
    decimal Units, decimal UnitPrice, decimal Fees = 0, string Currency = "EUR");

public sealed record LedgerPosition(decimal Units, decimal CostBasis, decimal RealizedPnl, decimal Dividends, decimal Fees);

public static class PortfolioLedger
{
    /// <summary>Weighted average cost including buy fees; no tax treatment or FX assumptions.</summary>
    public static LedgerPosition Calculate(IEnumerable<PortfolioTransaction> transactions)
    {
        decimal units=0, cost=0, realized=0, dividends=0, fees=0;
        foreach (var t in transactions.OrderBy(x => x.When))
        {
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
                    units *= t.Units;
                    break;
            }
        }
        return new LedgerPosition(units, cost, realized, dividends, fees);
    }
}
