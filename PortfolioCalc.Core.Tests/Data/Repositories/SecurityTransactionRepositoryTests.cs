using Microsoft.EntityFrameworkCore;
using PortfolioCalc.Core.Data;
using PortfolioCalc.Core.Data.Repositories;
using PortfolioCalc.Core.Domain;

namespace PortfolioCalc.Core.Tests.Data.Repositories;

public class SecurityTransactionRepositoryTests
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

    private static async Task<Position> CreatePosition(PortfolioDbContext context, string symbol = "AAPL")
    {
        var account = await new AccountRepository(context).AddAsync(new Account { Name = "IBKR Main" });
        var security = await new SecurityRepository(context).AddAsync(new Security { Symbol = symbol, Name = "Apple Inc.", Currency = "USD" });
        return await new PositionRepository(context).AddAsync(new Position { AccountId = account.Id, SecurityId = security.Id });
    }

    [Fact]
    public async Task AddAsync_then_GetByIdAsync_round_trips_a_security_transaction()
    {
        using var context = CreateOpenInMemoryContext();
        var position = await CreatePosition(context);
        var repository = new SecurityTransactionRepository(context);

        var added = await repository.AddAsync(new SecurityTransaction
        {
            PositionId = position.Id,
            Type = SecurityTransactionType.Buy,
            Date = new DateOnly(2026, 1, 15),
            Quantity = 10m,
            Amount = -1500m,
            Currency = "USD",
            FeeAmount = -1m,
            FeeCurrency = "USD",
        });

        var fetched = await repository.GetByIdAsync(added.Id);

        Assert.NotNull(fetched);
        Assert.Equal(SecurityTransactionType.Buy, fetched.Type);
        Assert.Equal(10m, fetched.Quantity);
        Assert.Equal(-1m, fetched.FeeAmount);
    }

    [Fact]
    public async Task UpdateAsync_persists_changes()
    {
        using var context = CreateOpenInMemoryContext();
        var position = await CreatePosition(context);
        var repository = new SecurityTransactionRepository(context);
        var added = await repository.AddAsync(new SecurityTransaction
        {
            PositionId = position.Id,
            Type = SecurityTransactionType.Buy,
            Date = new DateOnly(2026, 1, 15),
            Quantity = 10m,
            Amount = -1500m,
            Currency = "USD",
        });

        added.Quantity = 12m;
        await repository.UpdateAsync(added);

        var fetched = await repository.GetByIdAsync(added.Id);
        Assert.Equal(12m, fetched!.Quantity);
    }

    [Fact]
    public async Task DeleteAsync_removes_the_transaction()
    {
        using var context = CreateOpenInMemoryContext();
        var position = await CreatePosition(context);
        var repository = new SecurityTransactionRepository(context);
        var added = await repository.AddAsync(new SecurityTransaction
        {
            PositionId = position.Id,
            Type = SecurityTransactionType.Dividend,
            Date = new DateOnly(2026, 1, 15),
            Amount = 20m,
            Currency = "USD",
        });

        await repository.DeleteAsync(added.Id);

        Assert.Null(await repository.GetByIdAsync(added.Id));
    }

    [Fact]
    public async Task GetBySecurityAsync_aggregates_across_accounts_holding_the_same_security()
    {
        using var context = CreateOpenInMemoryContext();
        var accountRepository = new AccountRepository(context);
        var securityRepository = new SecurityRepository(context);
        var positionRepository = new PositionRepository(context);
        var repository = new SecurityTransactionRepository(context);

        var security = await securityRepository.AddAsync(new Security { Symbol = "AAPL", Name = "Apple Inc.", Currency = "USD" });
        var accountA = await accountRepository.AddAsync(new Account { Name = "Broker A" });
        var accountB = await accountRepository.AddAsync(new Account { Name = "Broker B" });
        var positionA = await positionRepository.AddAsync(new Position { AccountId = accountA.Id, SecurityId = security.Id });
        var positionB = await positionRepository.AddAsync(new Position { AccountId = accountB.Id, SecurityId = security.Id });

        await repository.AddAsync(new SecurityTransaction { PositionId = positionA.Id, Type = SecurityTransactionType.Buy, Date = new DateOnly(2026, 1, 1), Quantity = 1m, Amount = -100m, Currency = "USD" });
        await repository.AddAsync(new SecurityTransaction { PositionId = positionB.Id, Type = SecurityTransactionType.Buy, Date = new DateOnly(2026, 1, 2), Quantity = 2m, Amount = -200m, Currency = "USD" });

        var result = await repository.GetBySecurityAsync(security.Id);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task AddAsync_rejects_a_buy_without_quantity()
    {
        using var context = CreateOpenInMemoryContext();
        var position = await CreatePosition(context);
        var repository = new SecurityTransactionRepository(context);

        var invalid = new SecurityTransaction
        {
            PositionId = position.Id,
            Type = SecurityTransactionType.Buy,
            Date = new DateOnly(2026, 1, 1),
            Amount = -100m,
            Currency = "USD",
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.AddAsync(invalid));
    }
}
