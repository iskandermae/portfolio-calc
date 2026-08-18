namespace PortfolioCalc.Core.Validation;

/// <summary>Pure stddev-based outlier check for one FX-rate/price series — see
/// doc/stories/07-price-rate-quality-validation.md. Deliberately independent of the DB/
/// service plumbing so it can be unit-tested directly against synthetic series.</summary>
public static class AnomalyDetector
{
    /// <summary>Minimum number of recent historical points needed before a stddev is
    /// considered meaningful. Below this, a new value is never flagged — there isn't
    /// enough of a baseline to judge it against (e.g. the series' first value).</summary>
    public const int MinHistoryPoints = 5;

    /// <summary>How many stddevs from the recent mean a new value can fall before it's
    /// flagged. 3 is the conventional statistical-outlier cutoff (>99.7% of normally
    /// distributed values fall within 3 stddevs), which keeps ordinary day-to-day
    /// volatility from generating false positives while still catching real jumps.</summary>
    public const double ThresholdStdDevs = 3.0;

    /// <summary>How many trailing days of stored history a caller should gather to judge
    /// a new value against. 90 days gives a few months of typical volatility to compute a
    /// stddev from, without reaching back so far that a series' current regime (e.g. a
    /// currency that became more volatile) gets diluted by stale data.</summary>
    public const int TrailingWindowDays = 90;

    /// <summary>True if <paramref name="candidate"/> falls more than <see
    /// cref="ThresholdStdDevs"/> standard deviations from the mean of <paramref
    /// name="recentHistory"/>. Always false when there are fewer than <see
    /// cref="MinHistoryPoints"/> history points.</summary>
    public static bool IsAnomalous(IReadOnlyList<decimal> recentHistory, decimal candidate)
    {
        if (recentHistory.Count < MinHistoryPoints)
            return false;

        var mean = recentHistory.Average();
        var variance = recentHistory.Sum(v => (double)((v - mean) * (v - mean))) / recentHistory.Count;
        var stdDev = Math.Sqrt(variance);

        // A perfectly flat history (stddev 0) still means any change is unexpected.
        if (stdDev == 0)
            return candidate != mean;

        var deviation = Math.Abs((double)(candidate - mean));
        return deviation > ThresholdStdDevs * stdDev;
    }
}
