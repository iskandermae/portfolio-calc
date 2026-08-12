using Microsoft.EntityFrameworkCore;
using PortfolioCalc.Core.Domain;
using PortfolioCalc.Core.Repositories;

namespace PortfolioCalc.Core.Data.Repositories;

public class SecurityTransactionRepository(PortfolioDbContext context) : ISecurityTransactionRepository
{
    public async Task<SecurityTransaction> AddAsync(SecurityTransaction transaction)
    {
        transaction.Validate();
        context.SecurityTransactions.Add(transaction);
        await context.SaveChangesAsync();
        return transaction;
    }

    public Task<SecurityTransaction?> GetByIdAsync(int id) =>
        context.SecurityTransactions.FirstOrDefaultAsync(t => t.Id == id);

    public async Task UpdateAsync(SecurityTransaction transaction)
    {
        transaction.Validate();
        context.SecurityTransactions.Update(transaction);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await context.SecurityTransactions.FindAsync(id);
        if (entity is not null)
        {
            context.SecurityTransactions.Remove(entity);
            await context.SaveChangesAsync();
        }
    }

    public async Task<IReadOnlyList<SecurityTransaction>> GetByDateRangeAsync(DateOnly from, DateOnly to) =>
        await context.SecurityTransactions
            .Where(t => t.Date >= from && t.Date <= to)
            .OrderBy(t => t.Date)
            .ToListAsync();

    public async Task<IReadOnlyList<SecurityTransaction>> GetBySecurityAsync(int securityId) =>
        await context.SecurityTransactions
            .Where(t => t.Position!.SecurityId == securityId)
            .OrderBy(t => t.Date)
            .ToListAsync();

    public async Task<IReadOnlyList<SecurityTransaction>> GetByPositionAsync(int positionId) =>
        await context.SecurityTransactions
            .Where(t => t.PositionId == positionId)
            .OrderBy(t => t.Date)
            .ToListAsync();
}
