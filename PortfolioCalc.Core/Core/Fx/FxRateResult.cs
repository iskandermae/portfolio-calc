namespace PortfolioCalc.Core.Fx;

/// <summary>Result of an <see cref="IFxRateProvider"/> lookup. <see cref="Rate"/> is only
/// populated when <see cref="Status"/> is <see cref="FxRateStatus.Success"/>.</summary>
public sealed record FxRateResult(FxRateStatus Status, decimal? Rate, string? ErrorMessage = null)
{
    public static FxRateResult Ok(decimal rate) => new(FxRateStatus.Success, rate);

    public static FxRateResult Unsupported(string errorMessage) =>
        new(FxRateStatus.UnsupportedCurrency, null, errorMessage);

    public static FxRateResult NetworkFailure(string errorMessage) =>
        new(FxRateStatus.NetworkError, null, errorMessage);
}
