namespace PortfolioCalc.Core.Prices;

/// <summary>Fetches a security's price, in its own trading currency, for a given date.
/// Mirrors <see cref="PortfolioCalc.Core.Fx.IFxRateProvider"/>. See
/// <c>PortfolioCalc.Core.Data.Prices.YahooFinanceSecurityPriceProvider</c> for the concrete
/// implementation and doc/decisions.md for why it was chosen (doc/stories/04-store-reuse-prices-rates.md).</summary>
public interface ISecurityPriceProvider
{
    Task<PriceResult> GetPriceAsync(
        string symbol,
        string currency,
        DateOnly date,
        CancellationToken cancellationToken = default);
}
