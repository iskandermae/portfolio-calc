using PortfolioCalc.Core.Charting;

namespace PortfolioCalc.Core.Tests.Charting;

public class InflationAdjustmentCalculatorTests
{
    /// <summary>The story's own worked example (doc/stories/10-position-value-chart-report.md):
    /// a price on 01 Aug 2024, adjusted forward to 01 Sep 2026, using 2024=4%, 2025=6%,
    /// 2026=5%. 2024 is a leap year (366 days); 2026 the day count is against 365. Active
    /// days: 2024 -> 01.08.2024 to 01.01.2025 = 153 days; 2025 -> full year (exponent 1);
    /// 2026 -> 01.01.2026 to 01.09.2026 = 243 days.</summary>
    [Fact]
    public void ComputeForwardFactor_matches_the_storys_worked_example()
    {
        var from = new DateOnly(2024, 8, 1);
        var to = new DateOnly(2026, 9, 1);
        var rates = new Dictionary<int, decimal?> { [2024] = 0.04m, [2025] = 0.06m, [2026] = 0.05m };

        var factor = InflationAdjustmentCalculator.ComputeForwardFactor(from, to, y => rates[y]);

        var expected =
            Math.Pow(1.04, 153.0 / 366.0) *
            Math.Pow(1.06, 1.0) *
            Math.Pow(1.05, 243.0 / 365.0);

        Assert.NotNull(factor);
        Assert.Equal((decimal)expected, factor!.Value, precision: 10);
    }

    [Fact]
    public void ComputeForwardFactor_within_a_single_year_uses_one_partial_year_term()
    {
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 7, 1); // 181 days into a 365-day year
        var rates = new Dictionary<int, decimal?> { [2026] = 0.05m };

        var factor = InflationAdjustmentCalculator.ComputeForwardFactor(from, to, y => rates[y]);

        var expected = Math.Pow(1.05, 181.0 / 365.0);
        Assert.NotNull(factor);
        Assert.Equal((decimal)expected, factor!.Value, precision: 10);
    }

    [Fact]
    public void ComputeForwardFactor_returns_1_when_from_equals_to()
    {
        var date = new DateOnly(2026, 3, 15);
        var factor = InflationAdjustmentCalculator.ComputeForwardFactor(date, date, _ => 0.05m);

        Assert.Equal(1m, factor);
    }

    [Fact]
    public void ComputeForwardFactor_returns_null_when_any_years_rate_is_unresolvable()
    {
        var from = new DateOnly(2024, 8, 1);
        var to = new DateOnly(2026, 9, 1);
        var rates = new Dictionary<int, decimal?> { [2024] = 0.04m, [2025] = null, [2026] = 0.05m };

        var factor = InflationAdjustmentCalculator.ComputeForwardFactor(from, to, y => rates[y]);

        Assert.Null(factor);
    }

    [Fact]
    public void ComputeForwardFactor_throws_when_toDate_is_before_fromDate()
    {
        Assert.Throws<ArgumentException>(() => InflationAdjustmentCalculator.ComputeForwardFactor(
            new DateOnly(2026, 1, 1), new DateOnly(2025, 1, 1), _ => 0m));
    }
}
