namespace PortfolioCalc.Core.Inflation;

/// <summary>Outcome of an <see cref="IInflationRateProvider"/> fetch attempt. Mirrors
/// <see cref="PortfolioCalc.Core.Fx.FxRateStatus"/> — a failure is a distinct status rather
/// than an exception or a silently missing rate (see
/// doc/stories/06-import-inflation-rates.md).</summary>
public enum InflationRateStatus
{
    Success,
    UnsupportedCurrency,
    NetworkError
}
