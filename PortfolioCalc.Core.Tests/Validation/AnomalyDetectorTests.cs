using PortfolioCalc.Core.Validation;

namespace PortfolioCalc.Core.Tests.Validation;

public class AnomalyDetectorTests
{
    [Fact]
    public void IsAnomalous_returns_false_for_a_value_within_normal_variation()
    {
        // Tight, realistic day-to-day variation around 1.10.
        var history = new List<decimal> { 1.10m, 1.11m, 1.09m, 1.10m, 1.12m, 1.09m, 1.11m };

        var result = AnomalyDetector.IsAnomalous(history, 1.105m);

        Assert.False(result);
    }

    [Fact]
    public void IsAnomalous_returns_true_for_a_value_far_outside_recent_volatility()
    {
        var history = new List<decimal> { 1.10m, 1.11m, 1.09m, 1.10m, 1.12m, 1.09m, 1.11m };

        // An order-of-magnitude jump relative to the ~0.01 stddev of the history above.
        var result = AnomalyDetector.IsAnomalous(history, 2.20m);

        Assert.True(result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    public void IsAnomalous_returns_false_when_there_is_insufficient_history(int pointCount)
    {
        var history = Enumerable.Range(0, pointCount).Select(i => 1.10m + i * 0.5m).ToList();

        // Even a huge jump can't be judged without enough of a baseline.
        var result = AnomalyDetector.IsAnomalous(history, 100m);

        Assert.False(result);
    }

    [Fact]
    public void IsAnomalous_returns_false_for_the_exact_same_value_as_a_flat_history()
    {
        var history = new List<decimal> { 1.10m, 1.10m, 1.10m, 1.10m, 1.10m };

        var result = AnomalyDetector.IsAnomalous(history, 1.10m);

        Assert.False(result);
    }

    [Fact]
    public void IsAnomalous_returns_true_for_any_change_from_a_perfectly_flat_history()
    {
        var history = new List<decimal> { 1.10m, 1.10m, 1.10m, 1.10m, 1.10m };

        var result = AnomalyDetector.IsAnomalous(history, 1.11m);

        Assert.True(result);
    }
}
