namespace PortfolioCalc.Core.Analytics;

/// <summary>Pure synthetic-growth-rate ("CAGR") calc — see
/// doc/stories/11-position-performance-report.md. Solves <c>currentValue = initialInvestment
/// * (1 + r) ^ years</c> for r on a days-elapsed basis, annualized. Kept independent of the
/// DB/service layer so the formula is directly unit-testable.</summary>
public static class GrowthRateCalculator
{
    /// <summary>A transaction/position younger than this many days is annualized as if it
    /// were this old, to avoid wildly exaggerated rates for very recent cash flows (e.g. a
    /// 0.2% two-day gain naively annualizes to a triple-digit percentage). Per explicit
    /// business decision — see doc/stories/11-position-performance-report.md.</summary>
    public const int MinDaysElapsed = 10;

    /// <summary>Null when <paramref name="initialInvestment"/> isn't a positive amount to
    /// grow from — there's no meaningful rate to solve for.</summary>
    public static decimal? ComputeAnnualizedRate(
        decimal initialInvestment, decimal currentValue, int daysElapsed)
    {
        if (initialInvestment <= 0 || currentValue < 0)
            return null;

        var years = Math.Max(daysElapsed, MinDaysElapsed) / 365.25;
        var ratio = (double)(currentValue / initialInvestment);
        var rate = Math.Pow(ratio, 1.0 / years) - 1;
        return (decimal)rate;
    }
}
