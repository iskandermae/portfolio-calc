using PortfolioCalc.Core.Fx;

namespace PortfolioCalc.App.Application.Caching;

public class CachingFxRateProvider(IFxRateProvider inner, ExternalCallCache cache) : IFxRateProvider
{
    public Task<FxRateResult> GetRateAsync(
        string fromCurrency, string toCurrency, DateOnly date, CancellationToken cancellationToken = default) =>
        cache.GetOrAddAsync(
            $"fx|{fromCurrency}|{toCurrency}|{date:yyyy-MM-dd}",
            () => inner.GetRateAsync(fromCurrency, toCurrency, date, cancellationToken));
}
