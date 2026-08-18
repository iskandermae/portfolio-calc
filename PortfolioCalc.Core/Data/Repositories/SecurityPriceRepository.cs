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

    public async Task<IReadOnlyList<SecurityPrice>> GetPendingAsync() =>
        await context.SecurityPrices
            .Include(p => p.Security)
            .Where(p => p.Status == ValidationStatus.PendingValidation)
            .OrderBy(p => p.Date)
            .ToListAsync();

    public async Task UpdateStatusAsync(int id, ValidationStatus status, decimal? correctedPrice = null)
    {
        var price = await context.SecurityPrices.FindAsync(id)
            ?? throw new InvalidOperationException($"SecurityPrice {id} not found.");
        price.Status = status;
        if (correctedPrice is not null)
            price.Price = correctedPrice.Value;
        await context.SaveChangesAsync();
    }
}
