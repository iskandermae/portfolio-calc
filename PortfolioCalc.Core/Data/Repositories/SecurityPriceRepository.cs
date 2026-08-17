using Microsoft.EntityFrameworkCore;
using PortfolioCalc.Core.Domain;
using PortfolioCalc.Core.Repositories;

namespace PortfolioCalc.Core.Data.Repositories;

public class SecurityPriceRepository(PortfolioDbContext context) : ISecurityPriceRepository
{
    public Task<SecurityPrice?> GetAsync(int securityId, DateOnly date) =>
        context.SecurityPrices.FirstOrDefaultAsync(p => p.SecurityId == securityId && p.Date == date);

    public async Task<IReadOnlyList<SecurityPrice>> GetRangeAsync(int securityId, DateOnly from, DateOnly to) =>
        await context.SecurityPrices
            .Where(p => p.SecurityId == securityId && p.Date >= from && p.Date <= to)
            .OrderBy(p => p.Date)
            .ToListAsync();

    public async Task<SecurityPrice> AddAsync(SecurityPrice price)
    {
        context.SecurityPrices.Add(price);
        await context.SaveChangesAsync();
        return price;
    }
}
