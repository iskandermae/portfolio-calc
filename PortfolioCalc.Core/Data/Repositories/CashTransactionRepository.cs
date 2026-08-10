using Microsoft.EntityFrameworkCore;
using PortfolioCalc.Core.Domain;
using PortfolioCalc.Core.Repositories;

namespace PortfolioCalc.Core.Data.Repositories;

public class CashTransactionRepository(PortfolioDbContext context) : ICashTransactionRepository
{
    public async Task<CashTransaction> AddAsync(CashTransaction transaction)
    {
        transaction.Validate();
        context.CashTransactions.Add(transaction);
        await context.SaveChangesAsync();
        return transaction;
    }

    public Task<CashTransaction?> GetByIdAsync(int id) =>
        context.CashTransactions.FirstOrDefaultAsync(t => t.Id == id);

    public async Task UpdateAsync(CashTransaction transaction)
    {
        transaction.Validate();
        context.CashTransactions.Update(transaction);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await context.CashTransactions.FindAsync(id);
        if (entity is not null)
        {
            context.CashTransactions.Remove(entity);
            await context.SaveChangesAsync();
        }
    }

    public async Task<IReadOnlyList<CashTransaction>> GetByDateRangeAsync(DateOnly from, DateOnly to) =>
        await context.CashTransactions
            .Where(t => t.Date >= from && t.Date <= to)
            .OrderBy(t => t.Date)
            .ToListAsync();

    public async Task<IReadOnlyList<CashTransaction>> GetByAccountAsync(int accountId) =>
        await context.CashTransactions
            .Where(t => t.AccountId == accountId)
            .OrderBy(t => t.Date)
            .ToListAsync();
}
