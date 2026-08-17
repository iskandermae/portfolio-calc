namespace PortfolioCalc.Core.Fx;

/// <summary>Outcome of an FX rate fetch attempt. A failure is a distinct status rather than
/// an exception or a missing/null rate silently read as "no data" — see
/// doc/stories/03-fetch-cross-currency-rates.md.</summary>
public enum FxRateStatus
{
    Success,
    UnsupportedCurrency,
    NetworkError
}
