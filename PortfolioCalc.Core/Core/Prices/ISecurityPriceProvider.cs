namespace PortfolioCalc.Core.Prices;

/// <summary>Fetches a security's price, in its own trading currency, for a given date.
/// Mirrors <see cref="PortfolioCalc.Core.Fx.IFxRateProvider"/> — no concrete `Data/`
/// implementation exists yet (no price data source has been chosen); this interface exists
/// so the caching orchestration (<c>SecurityPriceService</c>) and storage schema can be
/// built now against a stable contract, per the open question in
/// doc/stories/04-store-reuse-prices-rates.md.</summary>
public interface ISecurityPriceProvider
{
    Task<PriceResult> GetPriceAsync(
        string symbol,
        string currency,
        DateOnly date,
        CancellationToken cancellationToken = default);
}
