using Microsoft.EntityFrameworkCore;
using PortfolioCalc.App.Application.Fx;
using PortfolioCalc.Core.Data;
using PortfolioCalc.Core.Data.Fx;
using PortfolioCalc.Core.Data.Repositories;
using PortfolioCalc.Core.Domain;
using PortfolioCalc.Core.Fx;
using PortfolioCalc.Core.Validation;

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

    [Fact]
    public async Task GetRateAsync_flags_a_new_rate_far_outside_recent_history_as_PendingValidation()
    {
        using var context = CreateOpenInMemoryContext();
        var repository = new FxRateRepository(context);
        for (var day = 1; day <= 6; day++)
        {
            await repository.AddAsync(new FxRate
            {
                FromCurrency = "USD", ToCurrency = "EUR",
                Date = new DateOnly(2026, 1, day), Rate = 0.90m + day * 0.001m,
            });
        }
        var provider = new FakeFxRateProvider(FxRateResult.Ok(5.00m));
        var service = new FxRateService(repository, provider);

        var result = await service.GetRateAsync("USD", "EUR", new DateOnly(2026, 1, 20));

        Assert.Equal(FxRateStatus.Success, result.Status);
        Assert.Equal(5.00m, result.Rate);
        var stored = await context.FxRates.SingleAsync(r => r.Date == new DateOnly(2026, 1, 20));
        Assert.Equal(ValidationStatus.PendingValidation, stored.Status);
    }

    [Fact]
    public async Task GetRateAsync_leaves_a_new_rate_consistent_with_recent_history_as_Valid()
    {
        using var context = CreateOpenInMemoryContext();
        var repository = new FxRateRepository(context);
        for (var day = 1; day <= 6; day++)
        {
            await repository.AddAsync(new FxRate
            {
                FromCurrency = "USD", ToCurrency = "EUR",
                Date = new DateOnly(2026, 1, day), Rate = 0.90m + day * 0.001m,
            });
        }
        var provider = new FakeFxRateProvider(FxRateResult.Ok(0.907m));
        var service = new FxRateService(repository, provider);

        await service.GetRateAsync("USD", "EUR", new DateOnly(2026, 1, 20));

        var stored = await context.FxRates.SingleAsync(r => r.Date == new DateOnly(2026, 1, 20));
        Assert.Equal(ValidationStatus.Valid, stored.Status);
    }

    [Fact]
    public async Task GetRateAsync_does_not_flag_a_new_rate_when_history_is_insufficient()
    {
        using var context = CreateOpenInMemoryContext();
        var repository = new FxRateRepository(context);
        var provider = new FakeFxRateProvider(FxRateResult.Ok(500m));
        var service = new FxRateService(repository, provider);

        await service.GetRateAsync("USD", "EUR", KnownPastDate);

        var stored = await context.FxRates.SingleAsync();
        Assert.Equal(ValidationStatus.Valid, stored.Status);
    }

    [Fact]
    public async Task GetRateAsync_ignores_a_stored_pending_rate_and_fetches_live_instead()
    {
        using var context = CreateOpenInMemoryContext();
        var repository = new FxRateRepository(context);
        await repository.AddAsync(new FxRate
        {
            FromCurrency = "USD", ToCurrency = "EUR", Date = KnownPastDate, Rate = 5.00m,
            Status = ValidationStatus.PendingValidation,
        });
        var provider = new FakeFxRateProvider(FxRateResult.Ok(0.91m));
        var service = new FxRateService(repository, provider);

        var result = await service.GetRateAsync("USD", "EUR", KnownPastDate);

        Assert.Equal(FxRateStatus.Success, result.Status);
        Assert.Equal(0.91m, result.Rate);
        Assert.Equal(1, provider.CallCount);
        // No second row was inserted for the same pair/date (would violate the unique index).
        Assert.Single(context.FxRates);
        var stored = await context.FxRates.SingleAsync();
        Assert.Equal(ValidationStatus.PendingValidation, stored.Status);
        Assert.Equal(5.00m, stored.Rate);
    }

    [Fact]
    public async Task GetHistoryAsync_excludes_rates_pending_validation()
    {
        using var context = CreateOpenInMemoryContext();
        var repository = new FxRateRepository(context);
        await repository.AddAsync(new FxRate
        {
            FromCurrency = "USD", ToCurrency = "EUR", Date = new DateOnly(2026, 1, 1), Rate = 0.90m,
            Status = ValidationStatus.Valid,
        });
        await repository.AddAsync(new FxRate
        {
            FromCurrency = "USD", ToCurrency = "EUR", Date = new DateOnly(2026, 1, 2), Rate = 5.00m,
            Status = ValidationStatus.PendingValidation,
        });
        var service = new FxRateService(repository, new FakeFxRateProvider(FxRateResult.Ok(1m)));

        var history = await service.GetHistoryAsync(
            "USD", "EUR", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2));

        Assert.Single(history);
        Assert.Equal(0.90m, history[0].Rate);
    }
}
