using Microsoft.EntityFrameworkCore;
using PortfolioCalc.Core.Data;
using PortfolioCalc.Core.Data.Repositories;
using PortfolioCalc.Core.Domain;

namespace PortfolioCalc.Core.Tests.Data.Repositories;

public class FxRateRepositoryTests
{
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

    [Fact]
    public async Task AddAsync_then_GetAsync_round_trips_a_rate()
    {
        using var context = CreateOpenInMemoryContext();
        var repository = new FxRateRepository(context);

        await repository.AddAsync(new FxRate
        {
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Date = new DateOnly(2026, 1, 15),
            Rate = 0.913m,
        });

        var fetched = await repository.GetAsync("USD", "EUR", new DateOnly(2026, 1, 15));

        Assert.NotNull(fetched);
        Assert.Equal(0.913m, fetched.Rate);
    }

    [Fact]
    public async Task GetAsync_returns_null_for_a_date_not_stored()
    {
        using var context = CreateOpenInMemoryContext();
        var repository = new FxRateRepository(context);

        var fetched = await repository.GetAsync("USD", "EUR", new DateOnly(2026, 1, 15));

        Assert.Null(fetched);
    }

    [Fact]
    public async Task GetRangeAsync_returns_only_rates_within_range_for_the_given_pair()
    {
        using var context = CreateOpenInMemoryContext();
        var repository = new FxRateRepository(context);
        await repository.AddAsync(new FxRate { FromCurrency = "USD", ToCurrency = "EUR", Date = new DateOnly(2026, 1, 1), Rate = 0.90m });
        await repository.AddAsync(new FxRate { FromCurrency = "USD", ToCurrency = "EUR", Date = new DateOnly(2026, 2, 1), Rate = 0.91m });
        await repository.AddAsync(new FxRate { FromCurrency = "USD", ToCurrency = "EUR", Date = new DateOnly(2026, 3, 1), Rate = 0.92m });
        await repository.AddAsync(new FxRate { FromCurrency = "GBP", ToCurrency = "EUR", Date = new DateOnly(2026, 2, 1), Rate = 1.15m });

        var result = await repository.GetRangeAsync("USD", "EUR", new DateOnly(2026, 1, 15), new DateOnly(2026, 2, 15));

        Assert.Single(result);
        Assert.Equal(0.91m, result[0].Rate);
    }

    [Fact]
    public async Task AddAsync_defaults_a_new_rate_to_Valid_status()
    {
        using var context = CreateOpenInMemoryContext();
        var repository = new FxRateRepository(context);

        var added = await repository.AddAsync(new FxRate
        {
            FromCurrency = "USD", ToCurrency = "EUR", Date = new DateOnly(2026, 1, 15), Rate = 0.91m,
        });

        Assert.Equal(ValidationStatus.Valid, added.Status);
    }

    [Fact]
    public async Task GetPendingAsync_returns_only_rates_pending_validation()
    {
        using var context = CreateOpenInMemoryContext();
        var repository = new FxRateRepository(context);
        await repository.AddAsync(new FxRate
        {
            FromCurrency = "USD", ToCurrency = "EUR", Date = new DateOnly(2026, 1, 1), Rate = 0.90m,
            Status = ValidationStatus.Valid,
        });
        var pending = await repository.AddAsync(new FxRate
        {
            FromCurrency = "USD", ToCurrency = "EUR", Date = new DateOnly(2026, 2, 1), Rate = 5.00m,
            Status = ValidationStatus.PendingValidation,
        });
        await repository.AddAsync(new FxRate
        {
            FromCurrency = "USD", ToCurrency = "EUR", Date = new DateOnly(2026, 3, 1), Rate = 0.92m,
            Status = ValidationStatus.Rejected,
        });

        var result = await repository.GetPendingAsync();

        Assert.Single(result);
        Assert.Equal(pending.Id, result[0].Id);
    }

    [Fact]
    public async Task UpdateStatusAsync_marks_a_rate_Valid_and_applies_a_correction()
    {
        using var context = CreateOpenInMemoryContext();
        var repository = new FxRateRepository(context);
        var rate = await repository.AddAsync(new FxRate
        {
            FromCurrency = "USD", ToCurrency = "EUR", Date = new DateOnly(2026, 1, 1), Rate = 5.00m,
            Status = ValidationStatus.PendingValidation,
        });

        await repository.UpdateStatusAsync(rate.Id, ValidationStatus.Valid, correctedRate: 0.91m);

        var updated = await repository.GetAsync("USD", "EUR", new DateOnly(2026, 1, 1));
        Assert.NotNull(updated);
        Assert.Equal(ValidationStatus.Valid, updated.Status);
        Assert.Equal(0.91m, updated.Rate);
    }

    [Fact]
    public async Task UpdateStatusAsync_can_reject_without_changing_the_rate()
    {
        using var context = CreateOpenInMemoryContext();
        var repository = new FxRateRepository(context);
        var rate = await repository.AddAsync(new FxRate
        {
            FromCurrency = "USD", ToCurrency = "EUR", Date = new DateOnly(2026, 1, 1), Rate = 5.00m,
            Status = ValidationStatus.PendingValidation,
        });

        await repository.UpdateStatusAsync(rate.Id, ValidationStatus.Rejected);

        var updated = await repository.GetAsync("USD", "EUR", new DateOnly(2026, 1, 1));
        Assert.NotNull(updated);
        Assert.Equal(ValidationStatus.Rejected, updated.Status);
        Assert.Equal(5.00m, updated.Rate);
    }
}
