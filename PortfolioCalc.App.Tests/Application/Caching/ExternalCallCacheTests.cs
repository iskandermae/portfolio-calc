using PortfolioCalc.App.Application.Caching;
using PortfolioCalc.Core.Fx;

namespace PortfolioCalc.App.Tests.Application.Caching;

public class ExternalCallCacheTests
{
    private class CountingFxProvider : IFxRateProvider
    {
        public int Calls { get; private set; }

        public Task<FxRateResult> GetRateAsync(
            string fromCurrency, string toCurrency, DateOnly date, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(FxRateResult.Ok(1.5m));
        }
    }

    [Fact]
    public async Task Repeated_lookup_hits_the_provider_once_until_cleared()
    {
        var inner = new CountingFxProvider();
        var cache = new ExternalCallCache();
        var provider = new CachingFxRateProvider(inner, cache);
        var date = new DateOnly(2024, 1, 2);

        await provider.GetRateAsync("USD", "EUR", date);
        await provider.GetRateAsync("USD", "EUR", date);
        Assert.Equal(1, inner.Calls);

        await provider.GetRateAsync("USD", "GBP", date);
        Assert.Equal(2, inner.Calls);

        cache.Clear();
        await provider.GetRateAsync("USD", "EUR", date);
        Assert.Equal(3, inner.Calls);
    }
}
