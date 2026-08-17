namespace PortfolioCalc.Core.Inflation;

/// <summary>Result of an <see cref="IInflationRateProvider"/> lookup. <see cref="Rate"/> is
/// only populated when <see cref="Status"/> is <see cref="InflationRateStatus.Success"/>.
/// Mirrors <see cref="PortfolioCalc.Core.Fx.FxRateResult"/>.</summary>
public sealed record InflationRateResult(InflationRateStatus Status, decimal? Rate, string? ErrorMessage = null)
{
    public static InflationRateResult Ok(decimal rate) => new(InflationRateStatus.Success, rate);

    public static InflationRateResult Unsupported(string errorMessage) =>
        new(InflationRateStatus.UnsupportedCurrency, null, errorMessage);

    public static InflationRateResult NetworkFailure(string errorMessage) =>
        new(InflationRateStatus.NetworkError, null, errorMessage);
}
