using PortfolioCalc.Core.Prices;

namespace PortfolioCalc.App.Application.Caching;

public class CachingSecurityPriceProvider(ISecurityPriceProvider inner, ExternalCallCache cache) : ISecurityPriceProvider
{
    public Task<PriceResult> GetPriceAsync(
        string symbol, string currency, DateOnly date, string? exchange = null, CancellationToken cancellationToken = default) =>
        cache.GetOrAddAsync(
            $"price|{symbol}|{currency}|{exchange}|{date:yyyy-MM-dd}",
            () => inner.GetPriceAsync(symbol, currency, date, exchange, cancellationToken));
}
