using Microsoft.EntityFrameworkCore;
using PortfolioCalc.App.Application.Prices;
using PortfolioCalc.Core.Data;
using PortfolioCalc.Core.Data.Repositories;
using PortfolioCalc.Core.Domain;
using PortfolioCalc.Core.Prices;

namespace PortfolioCalc.App.Tests.Application.Prices;

public class SecurityPriceServiceTests
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

    private sealed class FakeSecurityPriceProvider(PriceResult result) : ISecurityPriceProvider
    {
        public int CallCount { get; private set; }

        public Task<PriceResult> GetPriceAsync(
            string symbol, string currency, DateOnly date, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(result);
        }
    }

    [Fact]
    public async Task GetPriceAsync_fetches_once_then_reuses_the_stored_price()
    {
        using var context = CreateOpenInMemoryContext();
        var security = await new SecurityRepository(context).AddAsync(
            new Security { Symbol = "AAPL", Name = "AAPL", Currency = "USD" });
        var provider = new FakeSecurityPriceProvider(PriceResult.Ok(190.25m));
        var service = new SecurityPriceService(new SecurityPriceRepository(context), provider);
        var date = new DateOnly(2026, 1, 15);

        var first = await service.GetPriceAsync(security, date);
        var second = await service.GetPriceAsync(security, date);

        Assert.Equal(PriceStatus.Success, first.Status);
        Assert.Equal(first.Price, second.Price);
        Assert.Equal(1, provider.CallCount);
        Assert.Single(context.SecurityPrices);
    }

    [Fact]
    public async Task GetPriceAsync_does_not_cache_an_unsupported_result()
    {
        using var context = CreateOpenInMemoryContext();
        var security = await new SecurityRepository(context).AddAsync(
            new Security { Symbol = "AAPL", Name = "AAPL", Currency = "USD" });
        var provider = new FakeSecurityPriceProvider(PriceResult.Unsupported("nope"));
        var service = new SecurityPriceService(new SecurityPriceRepository(context), provider);

        var result = await service.GetPriceAsync(security, new DateOnly(2026, 1, 15));

        Assert.Equal(PriceStatus.UnsupportedSecurity, result.Status);
        Assert.Empty(context.SecurityPrices);
    }

    [Fact]
    public async Task GetHistoryAsync_delegates_to_the_repository_range_query()
    {
        using var context = CreateOpenInMemoryContext();
        var security = await new SecurityRepository(context).AddAsync(
            new Security { Symbol = "AAPL", Name = "AAPL", Currency = "USD" });
        var repository = new SecurityPriceRepository(context);
        var date = new DateOnly(2026, 1, 15);
        await repository.AddAsync(new SecurityPrice { SecurityId = security.Id, Date = date, Price = 190m });
        var service = new SecurityPriceService(repository, new FakeSecurityPriceProvider(PriceResult.Ok(1m)));

        var history = await service.GetHistoryAsync(security.Id, date, date);

        Assert.Single(history);
    }
}
