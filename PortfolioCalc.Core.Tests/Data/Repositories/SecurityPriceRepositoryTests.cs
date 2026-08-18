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

    [Fact]
    public async Task AddAsync_defaults_a_new_price_to_Valid_status()
    {
        using var context = CreateOpenInMemoryContext();
        var security = await new SecurityRepository(context).AddAsync(
            new Security { Symbol = "AAPL", Name = "AAPL", Currency = "USD" });
        var repository = new SecurityPriceRepository(context);

        var added = await repository.AddAsync(new SecurityPrice
        {
            SecurityId = security.Id, Date = new DateOnly(2026, 1, 15), Price = 190.25m,
        });

        Assert.Equal(ValidationStatus.Valid, added.Status);
    }

    [Fact]
    public async Task GetPendingAsync_returns_only_prices_pending_validation()
    {
        using var context = CreateOpenInMemoryContext();
        var security = await new SecurityRepository(context).AddAsync(
            new Security { Symbol = "AAPL", Name = "AAPL", Currency = "USD" });
        var repository = new SecurityPriceRepository(context);
        await repository.AddAsync(new SecurityPrice
        {
            SecurityId = security.Id, Date = new DateOnly(2026, 1, 1), Price = 190m, Status = ValidationStatus.Valid,
        });
        var pending = await repository.AddAsync(new SecurityPrice
        {
            SecurityId = security.Id, Date = new DateOnly(2026, 2, 1), Price = 9000m,
            Status = ValidationStatus.PendingValidation,
        });
        await repository.AddAsync(new SecurityPrice
        {
            SecurityId = security.Id, Date = new DateOnly(2026, 3, 1), Price = 200m,
            Status = ValidationStatus.Rejected,
        });

        var result = await repository.GetPendingAsync();

        Assert.Single(result);
        Assert.Equal(pending.Id, result[0].Id);
    }

    [Fact]
    public async Task UpdateStatusAsync_marks_a_price_Valid_and_applies_a_correction()
    {
        using var context = CreateOpenInMemoryContext();
        var security = await new SecurityRepository(context).AddAsync(
            new Security { Symbol = "AAPL", Name = "AAPL", Currency = "USD" });
        var repository = new SecurityPriceRepository(context);
        var price = await repository.AddAsync(new SecurityPrice
        {
            SecurityId = security.Id, Date = new DateOnly(2026, 1, 1), Price = 9000m,
            Status = ValidationStatus.PendingValidation,
        });

        await repository.UpdateStatusAsync(price.Id, ValidationStatus.Valid, correctedPrice: 190m);

        var updated = await repository.GetAsync(security.Id, new DateOnly(2026, 1, 1));
        Assert.NotNull(updated);
        Assert.Equal(ValidationStatus.Valid, updated.Status);
        Assert.Equal(190m, updated.Price);
    }
}
