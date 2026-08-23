using PortfolioCalc.Core.Analytics;

namespace PortfolioCalc.Core.Tests.Analytics;

public class AverageCostCalculatorTests
{
    [Fact]
    public void Compute_blends_a_single_lot()
    {
        var result = AverageCostCalculator.Compute([new(10m, 1000m, 900m)]);

        Assert.NotNull(result);
        Assert.Equal(100m, result!.PerShareInSecurityCurrency);
        Assert.Equal(90m, result.PerShareInBaseCurrency);
    }

    [Fact]
    public void Compute_blends_multiple_lots_bought_at_different_prices_and_rates()
    {
        var lots = new[]
        {
            new AverageCostCalculator.Lot(5m, 500m, 450m),   // $100/share, rate 0.9
            new AverageCostCalculator.Lot(5m, 600m, 570m),   // $120/share, rate 0.95
        };

        var result = AverageCostCalculator.Compute(lots);

        Assert.NotNull(result);
        // (500 + 600) / 10 = 110 per share; (450 + 570) / 10 = 102 per share.
        Assert.Equal(110m, result!.PerShareInSecurityCurrency);
        Assert.Equal(102m, result.PerShareInBaseCurrency);
    }

    [Fact]
    public void Compute_returns_null_when_there_is_no_quantity_to_average_over()
    {
        var result = AverageCostCalculator.Compute([]);

        Assert.Null(result);
    }
}
