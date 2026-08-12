using Microsoft.EntityFrameworkCore;
using PortfolioCalc.Core.Data;
using PortfolioCalc.Core.Data.Repositories;
using PortfolioCalc.Core.Domain;

namespace PortfolioCalc.Core.Tests.Data.Repositories;

public class CashTransactionRepositoryTests
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
    public async Task AddAsync_then_GetByIdAsync_round_trips_a_cash_transaction()
    {
        using var context = CreateOpenInMemoryContext();
        var account = await new AccountRepository(context).AddAsync(new Account { Name = "IBKR Main" });
        var repository = new CashTransactionRepository(context);

        var added = await repository.AddAsync(new CashTransaction
        {
            AccountId = account.Id,
            Type = CashTransactionType.Deposit,
            Date = new DateOnly(2026, 1, 15),
            Amount = 500m,
            Currency = "USD",
        });

        var fetched = await repository.GetByIdAsync(added.Id);

        Assert.NotNull(fetched);
        Assert.Equal(CashTransactionType.Deposit, fetched.Type);
        Assert.Equal(500m, fetched.Amount);
        Assert.Equal("USD", fetched.Currency);
    }

    [Fact]
    public async Task UpdateAsync_persists_changes()
    {
        using var context = CreateOpenInMemoryContext();
        var account = await new AccountRepository(context).AddAsync(new Account { Name = "IBKR Main" });
        var repository = new CashTransactionRepository(context);
        var added = await repository.AddAsync(new CashTransaction
        {
            AccountId = account.Id,
            Type = CashTransactionType.Deposit,
            Date = new DateOnly(2026, 1, 15),
            Amount = 500m,
            Currency = "USD",
        });

        added.Amount = 750m;
        await repository.UpdateAsync(added);

        var fetched = await repository.GetByIdAsync(added.Id);
        Assert.Equal(750m, fetched!.Amount);
    }

    [Fact]
    public async Task DeleteAsync_removes_the_transaction()
    {
        using var context = CreateOpenInMemoryContext();
        var account = await new AccountRepository(context).AddAsync(new Account { Name = "IBKR Main" });
        var repository = new CashTransactionRepository(context);
        var added = await repository.AddAsync(new CashTransaction
        {
            AccountId = account.Id,
            Type = CashTransactionType.Withdrawal,
            Date = new DateOnly(2026, 1, 15),
            Amount = -100m,
            Currency = "USD",
        });

        await repository.DeleteAsync(added.Id);

        Assert.Null(await repository.GetByIdAsync(added.Id));
    }

    [Fact]
    public async Task GetByDateRangeAsync_returns_only_transactions_within_range()
    {
        using var context = CreateOpenInMemoryContext();
        var account = await new AccountRepository(context).AddAsync(new Account { Name = "IBKR Main" });
        var repository = new CashTransactionRepository(context);
        await repository.AddAsync(new CashTransaction { AccountId = account.Id, Type = CashTransactionType.Deposit, Date = new DateOnly(2026, 1, 1), Amount = 1m, Currency = "USD" });
        await repository.AddAsync(new CashTransaction { AccountId = account.Id, Type = CashTransactionType.Deposit, Date = new DateOnly(2026, 2, 1), Amount = 2m, Currency = "USD" });
        await repository.AddAsync(new CashTransaction { AccountId = account.Id, Type = CashTransactionType.Deposit, Date = new DateOnly(2026, 3, 1), Amount = 3m, Currency = "USD" });

        var result = await repository.GetByDateRangeAsync(new DateOnly(2026, 1, 15), new DateOnly(2026, 2, 15));

        Assert.Single(result);
        Assert.Equal(2m, result[0].Amount);
    }

    [Fact]
    public async Task AddAsync_rejects_an_invalid_transaction()
    {
        using var context = CreateOpenInMemoryContext();
        var account = await new AccountRepository(context).AddAsync(new Account { Name = "IBKR Main" });
        var repository = new CashTransactionRepository(context);

        var invalid = new CashTransaction
        {
            AccountId = account.Id,
            Type = CashTransactionType.Deposit,
            Date = new DateOnly(2026, 1, 1),
            Amount = -1m,
            Currency = "USD",
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.AddAsync(invalid));
    }
}
