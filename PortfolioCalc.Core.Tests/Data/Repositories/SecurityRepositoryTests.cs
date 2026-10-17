using Microsoft.EntityFrameworkCore;
using PortfolioCalc.Core.Data;
using PortfolioCalc.Core.Data.Repositories;
using PortfolioCalc.Core.Domain;

namespace PortfolioCalc.Core.Tests.Data.Repositories;

public class SecurityRepositoryTests
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
    public async Task AddAsync_persists_the_exchange_column()
    {
        using var context = CreateOpenInMemoryContext();
        var repository = new SecurityRepository(context);

        var added = await repository.AddAsync(new Security
        {
            Symbol = "AVSG", Name = "AVSG", Currency = "GBP", Exchange = "LSEETF",
        });

        var reloaded = await repository.GetByIdAsync(added.Id);
        Assert.NotNull(reloaded);
        Assert.Equal("LSEETF", reloaded!.Exchange);
    }

    [Fact]
    public async Task AddAsync_allows_a_null_exchange()
    {
        using var context = CreateOpenInMemoryContext();
        var repository = new SecurityRepository(context);

        var added = await repository.AddAsync(new Security { Symbol = "AAPL", Name = "AAPL", Currency = "USD" });

        var reloaded = await repository.GetByIdAsync(added.Id);
        Assert.NotNull(reloaded);
        Assert.Null(reloaded!.Exchange);
    }
}
