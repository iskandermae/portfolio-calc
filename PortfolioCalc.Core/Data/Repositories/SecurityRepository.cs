using Microsoft.EntityFrameworkCore;
using PortfolioCalc.Core.Domain;
using PortfolioCalc.Core.Repositories;

namespace PortfolioCalc.Core.Data.Repositories;

public class SecurityRepository(PortfolioDbContext context) : ISecurityRepository
{
    public async Task<Security> AddAsync(Security security)
    {
        context.Securities.Add(security);
        await context.SaveChangesAsync();
        return security;
    }

    public Task<Security?> GetByIdAsync(int id) =>
        context.Securities.FirstOrDefaultAsync(s => s.Id == id);

    public Task<Security?> GetBySymbolAndCurrencyAsync(string symbol, string currency) =>
        context.Securities.FirstOrDefaultAsync(s => s.Symbol == symbol && s.Currency == currency);

    public async Task UpdateAsync(Security security)
    {
        context.Securities.Update(security);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await context.Securities.FindAsync(id);
        if (entity is not null)
        {
            context.Securities.Remove(entity);
            await context.SaveChangesAsync();
        }
    }
}
