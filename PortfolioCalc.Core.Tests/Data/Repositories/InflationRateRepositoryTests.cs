using Microsoft.EntityFrameworkCore;
using PortfolioCalc.Core.Data;
using PortfolioCalc.Core.Data.Repositories;
using PortfolioCalc.Core.Domain;

namespace PortfolioCalc.Core.Tests.Data.Repositories;

public class InflationRateRepositoryTests
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
        var repository = new InflationRateRepository(context);

        await repository.AddAsync(new InflationRate
        {
            BaseCurrency = "USD",
            Period = new DateOnly(2024, 1, 1),
            Rate = 3.2m,
        });

        var fetched = await repository.GetAsync("USD", new DateOnly(2024, 1, 1));

        Assert.NotNull(fetched);
        Assert.Equal(3.2m, fetched.Rate);
    }

    [Fact]
    public async Task GetAsync_returns_null_for_a_period_not_stored()
    {
        using var context = CreateOpenInMemoryContext();
        var repository = new InflationRateRepository(context);

        var fetched = await repository.GetAsync("USD", new DateOnly(2024, 1, 1));

        Assert.Null(fetched);
    }

    [Fact]
    public async Task GetRangeAsync_returns_only_rates_within_range_for_the_given_currency()
    {
        using var context = CreateOpenInMemoryContext();
        var repository = new InflationRateRepository(context);
        await repository.AddAsync(new InflationRate { BaseCurrency = "USD", Period = new DateOnly(2022, 1, 1), Rate = 8.0m });
        await repository.AddAsync(new InflationRate { BaseCurrency = "USD", Period = new DateOnly(2023, 1, 1), Rate = 4.1m });
        await repository.AddAsync(new InflationRate { BaseCurrency = "USD", Period = new DateOnly(2024, 1, 1), Rate = 3.2m });
        await repository.AddAsync(new InflationRate { BaseCurrency = "EUR", Period = new DateOnly(2023, 1, 1), Rate = 5.5m });

        var result = await repository.GetRangeAsync("USD", new DateOnly(2023, 1, 1), new DateOnly(2024, 12, 31));

        Assert.Equal(2, result.Count);
        Assert.Equal(4.1m, result[0].Rate);
        Assert.Equal(3.2m, result[1].Rate);
    }
}
