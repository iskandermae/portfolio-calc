using Microsoft.EntityFrameworkCore;
using PortfolioCalc.App.Application.Inflation;
using PortfolioCalc.Core.Data;
using PortfolioCalc.Core.Data.Inflation;
using PortfolioCalc.Core.Data.Repositories;
using PortfolioCalc.Core.Inflation;

namespace PortfolioCalc.App.Tests.Application.Inflation;

public class InflationRateServiceTests
{
    private static readonly DateOnly KnownPastPeriod = new(2021, 1, 1);

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
    /// already-stored period makes no second external call while still exercising the real
    /// World Bank API for the first fetch.</summary>
    private sealed class CountingInflationRateProvider(IInflationRateProvider inner) : IInflationRateProvider
    {
        public int CallCount { get; private set; }

        public Task<InflationRateResult> GetRateAsync(
            string baseCurrency, DateOnly period, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return inner.GetRateAsync(baseCurrency, period, cancellationToken);
        }
    }

    [Fact]
    public async Task GetRateAsync_fetches_once_from_the_real_provider_then_reuses_the_stored_rate()
    {
        using var context = CreateOpenInMemoryContext();
        var provider = new CountingInflationRateProvider(new WorldBankInflationRateProvider(new HttpClient()));
        var service = new InflationRateService(new InflationRateRepository(context), provider);

        var first = await service.GetRateAsync("USD", KnownPastPeriod);
        var second = await service.GetRateAsync("USD", KnownPastPeriod);

        Assert.Equal(InflationRateStatus.Success, first.Status);
        Assert.Equal(first.Rate, second.Rate);
        Assert.Equal(1, provider.CallCount);
        Assert.Single(context.InflationRates);
    }

    private sealed class FakeInflationRateProvider(InflationRateResult result) : IInflationRateProvider
    {
        public int CallCount { get; private set; }

        public Task<InflationRateResult> GetRateAsync(
            string baseCurrency, DateOnly period, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(result);
        }
    }

    [Fact]
    public async Task GetRateAsync_does_not_cache_an_unsupported_or_network_failure_result()
    {
        using var context = CreateOpenInMemoryContext();
        var provider = new FakeInflationRateProvider(InflationRateResult.Unsupported("nope"));
        var service = new InflationRateService(new InflationRateRepository(context), provider);

        var result = await service.GetRateAsync("ZZZ", KnownPastPeriod);

        Assert.Equal(InflationRateStatus.UnsupportedCurrency, result.Status);
        Assert.Empty(context.InflationRates);
    }

    [Fact]
    public async Task GetRateAsync_network_failure_is_not_cached()
    {
        using var context = CreateOpenInMemoryContext();
        var provider = new FakeInflationRateProvider(InflationRateResult.NetworkFailure("down"));
        var service = new InflationRateService(new InflationRateRepository(context), provider);

        var result = await service.GetRateAsync("USD", KnownPastPeriod);

        Assert.Equal(InflationRateStatus.NetworkError, result.Status);
        Assert.Empty(context.InflationRates);
    }

    [Fact]
    public async Task GetHistoryAsync_delegates_to_the_repository_range_query()
    {
        using var context = CreateOpenInMemoryContext();
        var repository = new InflationRateRepository(context);
        await repository.AddAsync(new Core.Domain.InflationRate
        {
            BaseCurrency = "USD", Period = KnownPastPeriod, Rate = 4.7m,
        });
        var service = new InflationRateService(repository, new FakeInflationRateProvider(InflationRateResult.Ok(1m)));

        var history = await service.GetHistoryAsync("USD", KnownPastPeriod, KnownPastPeriod);

        Assert.Single(history);
    }
}
