using Microsoft.EntityFrameworkCore;
using PortfolioCalc.App.Application.Fx;
using PortfolioCalc.Core.Data;
using PortfolioCalc.Core.Data.Fx;
using PortfolioCalc.Core.Data.Repositories;
using PortfolioCalc.Core.Fx;

namespace PortfolioCalc.App.Tests.Application.Fx;

public class FxRateServiceTests
{
    private static readonly DateOnly KnownPastDate = new(2024, 1, 15);

    private static PortfolioDbContext CreateOpenInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<PortfolioDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var context = new PortfolioDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }

    /// <summary>Counts calls made to the wrapped provider, so a test can assert an
    /// already-stored date makes no second external call (per AC in
    /// doc/stories/04-store-reuse-prices-rates.md) while still exercising the real
    /// Frankfurter API for the first fetch.</summary>
    private sealed class CountingFxRateProvider(IFxRateProvider inner) : IFxRateProvider
    {
        public int CallCount { get; private set; }

        public Task<FxRateResult> GetRateAsync(
            string fromCurrency, string toCurrency, DateOnly date, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return inner.GetRateAsync(fromCurrency, toCurrency, date, cancellationToken);
        }
    }

    [Fact]
    public async Task GetRateAsync_fetches_once_from_the_real_provider_then_reuses_the_stored_rate()
    {
        using var context = CreateOpenInMemoryContext();
        var provider = new CountingFxRateProvider(new FrankfurterFxRateProvider(new HttpClient()));
        var service = new FxRateService(new FxRateRepository(context), provider);

        var first = await service.GetRateAsync("USD", "EUR", KnownPastDate);
        var second = await service.GetRateAsync("USD", "EUR", KnownPastDate);

        Assert.Equal(FxRateStatus.Success, first.Status);
        Assert.Equal(first.Rate, second.Rate);
        Assert.Equal(1, provider.CallCount);
        Assert.Single(context.FxRates);
    }

    private sealed class FakeFxRateProvider(FxRateResult result) : IFxRateProvider
    {
        public int CallCount { get; private set; }

        public Task<FxRateResult> GetRateAsync(
            string fromCurrency, string toCurrency, DateOnly date, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(result);
        }
    }

    [Fact]
    public async Task GetRateAsync_same_currency_returns_one_without_touching_storage_or_provider()
    {
        using var context = CreateOpenInMemoryContext();
        var provider = new FakeFxRateProvider(FxRateResult.Ok(0.5m));
        var service = new FxRateService(new FxRateRepository(context), provider);

        var result = await service.GetRateAsync("USD", "USD", KnownPastDate);

        Assert.Equal(1m, result.Rate);
        Assert.Equal(0, provider.CallCount);
        Assert.Empty(context.FxRates);
    }

    [Fact]
    public async Task GetRateAsync_does_not_cache_an_unsupported_or_network_failure_result()
    {
        using var context = CreateOpenInMemoryContext();
        var provider = new FakeFxRateProvider(FxRateResult.Unsupported("nope"));
        var service = new FxRateService(new FxRateRepository(context), provider);

        var result = await service.GetRateAsync("USD", "ZZZ", KnownPastDate);

        Assert.Equal(FxRateStatus.UnsupportedCurrency, result.Status);
        Assert.Empty(context.FxRates);
    }

    [Fact]
    public async Task GetHistoryAsync_delegates_to_the_repository_range_query()
    {
        using var context = CreateOpenInMemoryContext();
        var repository = new FxRateRepository(context);
        await repository.AddAsync(new Core.Domain.FxRate
        {
            FromCurrency = "USD", ToCurrency = "EUR", Date = KnownPastDate, Rate = 0.9m,
        });
        var service = new FxRateService(repository, new FakeFxRateProvider(FxRateResult.Ok(1m)));

        var history = await service.GetHistoryAsync("USD", "EUR", KnownPastDate, KnownPastDate);

        Assert.Single(history);
    }
}
