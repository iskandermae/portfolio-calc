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
}
