namespace PortfolioCalc.Core.Prices;

/// <summary>Fetches a security's price, in its own trading currency, for a given date.
/// Mirrors <see cref="PortfolioCalc.Core.Fx.IFxRateProvider"/>. See
/// <c>PortfolioCalc.Core.Data.Prices.YahooFinanceSecurityPriceProvider</c> for the concrete
/// implementation and doc/decisions.md for why it was chosen (doc/stories/04-store-reuse-prices-rates.md).</summary>
public interface ISecurityPriceProvider
{
    /// <param name="exchange">The security's raw broker listing-exchange code (e.g.
    /// IBKR's "LSEETF"), if known — null/empty when not recorded. A concrete provider
    /// may use this to resolve a market-specific symbol (e.g. a Yahoo ".L" suffix)
    /// instead of guessing from symbol/currency alone. See doc/decisions.md.</param>
    Task<PriceResult> GetPriceAsync(
        string symbol,
        string currency,
        DateOnly date,
        string? exchange = null,
        CancellationToken cancellationToken = default);
}
