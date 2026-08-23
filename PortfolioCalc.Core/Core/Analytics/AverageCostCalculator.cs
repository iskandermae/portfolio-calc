namespace PortfolioCalc.Core.Analytics;

/// <summary>Pure average-cost-basis calc for the tax-estimation report — see
/// doc/stories/12-tax-estimation-report.md. Blends every contributing buy lot (each already
/// converted to base currency at its own transaction date's FX rate) into one per-share cost,
/// rather than tracking individual lots (FIFO/LIFO/specific-lot).</summary>
public static class AverageCostCalculator
{
    /// <param name="Quantity">Shares acquired by this lot (a Buy or a TransferIn).</param>
    /// <param name="AmountInSecurityCurrency">Positive cost paid for <paramref name="Quantity"/>
    /// shares, in the security's own currency.</param>
    /// <param name="AmountInBaseCurrency">The same cost, converted to base currency at this
    /// lot's own transaction date.</param>
    public sealed record Lot(decimal Quantity, decimal AmountInSecurityCurrency, decimal AmountInBaseCurrency);

    public sealed record AverageCost(decimal PerShareInSecurityCurrency, decimal PerShareInBaseCurrency);

    /// <summary>Null if there's no quantity to average over — there's no meaningful
    /// per-share cost to solve for.</summary>
    public static AverageCost? Compute(IReadOnlyList<Lot> lots)
    {
        var totalQuantity = lots.Sum(l => l.Quantity);
        if (totalQuantity <= 0)
            return null;

        var totalSecurityCurrency = lots.Sum(l => l.AmountInSecurityCurrency);
        var totalBaseCurrency = lots.Sum(l => l.AmountInBaseCurrency);
        return new AverageCost(totalSecurityCurrency / totalQuantity, totalBaseCurrency / totalQuantity);
    }
}
