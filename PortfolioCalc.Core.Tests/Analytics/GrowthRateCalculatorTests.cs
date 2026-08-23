using PortfolioCalc.Core.Analytics;

namespace PortfolioCalc.Core.Tests.Analytics;

public class GrowthRateCalculatorTests
{
    // Days chosen as exact multiples of 365.25 (the calculator's year length) so the expected
    // rate comes out to a clean 10% instead of accumulating leap-year rounding noise.

    [Fact]
    public void ComputeAnnualizedRate_matches_a_simple_four_year_growth()
    {
        var rate = GrowthRateCalculator.ComputeAnnualizedRate(1000m, 1464.1m, 1461);

        Assert.NotNull(rate);
        Assert.Equal(0.1m, rate!.Value, 4);
    }

    [Fact]
    public void ComputeAnnualizedRate_matches_an_eight_year_growth()
    {
        // 1.1^8 = 2.14358881 (10% per year, compounded over eight years).
        var rate = GrowthRateCalculator.ComputeAnnualizedRate(1000m, 2143.58881m, 2922);

        Assert.NotNull(rate);
        Assert.Equal(0.1m, rate!.Value, 4);
    }

    [Fact]
    public void ComputeAnnualizedRate_floors_days_elapsed_at_the_minimum()
    {
        var atTwoDays = GrowthRateCalculator.ComputeAnnualizedRate(1000m, 1010m, 2);
        var atTheFloor = GrowthRateCalculator.ComputeAnnualizedRate(1000m, 1010m, GrowthRateCalculator.MinDaysElapsed);

        Assert.Equal(atTheFloor, atTwoDays);
    }

    [Fact]
    public void ComputeAnnualizedRate_does_not_floor_days_elapsed_above_the_minimum()
    {
        var atTheFloor = GrowthRateCalculator.ComputeAnnualizedRate(1000m, 1010m, GrowthRateCalculator.MinDaysElapsed);
        var wellAbove = GrowthRateCalculator.ComputeAnnualizedRate(1000m, 1010m, 365);

        Assert.NotEqual(atTheFloor, wellAbove);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void ComputeAnnualizedRate_returns_null_for_a_nonpositive_initial_investment(decimal initialInvestment)
    {
        var rate = GrowthRateCalculator.ComputeAnnualizedRate(initialInvestment, 100m, 100);

        Assert.Null(rate);
    }

    [Fact]
    public void ComputeAnnualizedRate_returns_a_negative_hundred_percent_for_a_total_loss()
    {
        var rate = GrowthRateCalculator.ComputeAnnualizedRate(1000m, 0m, 365);

        Assert.NotNull(rate);
        Assert.Equal(-1m, rate!.Value, 4);
    }
}
