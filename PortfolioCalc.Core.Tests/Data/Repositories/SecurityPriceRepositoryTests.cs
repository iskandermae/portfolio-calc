using Microsoft.EntityFrameworkCore;
using PortfolioCalc.Core.Data;
using PortfolioCalc.Core.Data.Repositories;
using PortfolioCalc.Core.Domain;

namespace PortfolioCalc.Core.Tests.Data.Repositories;

public class SecurityPriceRepositoryTests
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
    public async Task AddAsync_then_GetAsync_round_trips_a_price()
    {
        using var context = CreateOpenInMemoryContext();
        var security = await new SecurityRepository(context).AddAsync(
            new Security { Symbol = "AAPL", Name = "AAPL", Currency = "USD" });
        var repository = new SecurityPriceRepository(context);

        await repository.AddAsync(new SecurityPrice
        {
            SecurityId = security.Id,
            Date = new DateOnly(2026, 1, 15),
            Price = 190.25m,
        });

        var fetched = await repository.GetAsync(security.Id, new DateOnly(2026, 1, 15));

        Assert.NotNull(fetched);
        Assert.Equal(190.25m, fetched.Price);
    }

    [Fact]
    public async Task GetAsync_returns_null_for_a_date_not_stored()
    {
        using var context = CreateOpenInMemoryContext();
        var security = await new SecurityRepository(context).AddAsync(
            new Security { Symbol = "AAPL", Name = "AAPL", Currency = "USD" });
        var repository = new SecurityPriceRepository(context);

        var fetched = await repository.GetAsync(security.Id, new DateOnly(2026, 1, 15));

        Assert.Null(fetched);
    }

    [Fact]
    public async Task GetRangeAsync_returns_only_prices_within_range_for_the_given_security()
    {
        using var context = CreateOpenInMemoryContext();
        var securityRepository = new SecurityRepository(context);
        var aapl = await securityRepository.AddAsync(new Security { Symbol = "AAPL", Name = "AAPL", Currency = "USD" });
        var msft = await securityRepository.AddAsync(new Security { Symbol = "MSFT", Name = "MSFT", Currency = "USD" });
        var repository = new SecurityPriceRepository(context);
        await repository.AddAsync(new SecurityPrice { SecurityId = aapl.Id, Date = new DateOnly(2026, 1, 1), Price = 180m });
        await repository.AddAsync(new SecurityPrice { SecurityId = aapl.Id, Date = new DateOnly(2026, 2, 1), Price = 190m });
        await repository.AddAsync(new SecurityPrice { SecurityId = aapl.Id, Date = new DateOnly(2026, 3, 1), Price = 200m });
        await repository.AddAsync(new SecurityPrice { SecurityId = msft.Id, Date = new DateOnly(2026, 2, 1), Price = 400m });

        var result = await repository.GetRangeAsync(aapl.Id, new DateOnly(2026, 1, 15), new DateOnly(2026, 2, 15));

        Assert.Single(result);
        Assert.Equal(190m, result[0].Price);
    }
}
