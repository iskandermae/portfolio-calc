namespace PortfolioCalc.Core.Fx;

/// <summary>Fetches the exchange rate between two currencies for a given date.
/// Implementations live in Data/Fx/ (one per rate source). Calling code depends only on
/// this interface, so a different source can be swapped in — globally, or per currency via
/// a composite implementation — without any caller changing (see
/// doc/stories/03-fetch-cross-currency-rates.md). This interface covers fetching only; local
/// caching/reuse and suspicious-value flagging are separate follow-on stories.</summary>
public interface IFxRateProvider
{
    Task<FxRateResult> GetRateAsync(
        string fromCurrency,
        string toCurrency,
        DateOnly date,
        CancellationToken cancellationToken = default);
}
