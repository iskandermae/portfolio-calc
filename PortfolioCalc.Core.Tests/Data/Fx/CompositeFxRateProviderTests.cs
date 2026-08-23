using PortfolioCalc.Core.Data.Fx;
using PortfolioCalc.Core.Fx;

namespace PortfolioCalc.Core.Tests.Data.Fx;

public class CompositeFxRateProviderTests
{
    private sealed class RecordingFxRateProvider : IFxRateProvider
    {
        public bool WasCalled { get; private set; }

        public Task<FxRateResult> GetRateAsync(
            string fromCurrency, string toCurrency, DateOnly date, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(FxRateResult.Ok(1m));
        }
    }

    [Theory]
    [InlineData("EUR", "UAH")]
    [InlineData("UAH", "USD")]
    [InlineData("uah", "usd")]
    public async Task GetRateAsync_routes_a_UAH_involving_pair_to_the_UAH_provider(string fromCurrency, string toCurrency)
    {
        var defaultProvider = new RecordingFxRateProvider();
        var uahProvider = new RecordingFxRateProvider();
        var composite = new CompositeFxRateProvider(defaultProvider, uahProvider);

        await composite.GetRateAsync(fromCurrency, toCurrency, new DateOnly(2020, 3, 2));

        Assert.True(uahProvider.WasCalled);
        Assert.False(defaultProvider.WasCalled);
    }

    [Fact]
    public async Task GetRateAsync_routes_a_non_UAH_pair_to_the_default_provider()
    {
        var defaultProvider = new RecordingFxRateProvider();
        var uahProvider = new RecordingFxRateProvider();
        var composite = new CompositeFxRateProvider(defaultProvider, uahProvider);

        await composite.GetRateAsync("USD", "EUR", new DateOnly(2020, 3, 2));

        Assert.True(defaultProvider.WasCalled);
        Assert.False(uahProvider.WasCalled);
    }
}
