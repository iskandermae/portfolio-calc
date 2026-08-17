using Microsoft.EntityFrameworkCore;
using PortfolioCalc.Core.Domain;
using PortfolioCalc.Core.Repositories;

namespace PortfolioCalc.Core.Data.Repositories;

public class InflationRateRepository(PortfolioDbContext context) : IInflationRateRepository
{
    public Task<InflationRate?> GetAsync(string baseCurrency, DateOnly period) =>
        context.InflationRates.FirstOrDefaultAsync(r =>
            r.BaseCurrency == baseCurrency && r.Period == period);

    public async Task<IReadOnlyList<InflationRate>> GetRangeAsync(
        string baseCurrency, DateOnly from, DateOnly to) =>
        await context.InflationRates
            .Where(r => r.BaseCurrency == baseCurrency && r.Period >= from && r.Period <= to)
            .OrderBy(r => r.Period)
            .ToListAsync();

    public async Task<InflationRate> AddAsync(InflationRate rate)
    {
        context.InflationRates.Add(rate);
        await context.SaveChangesAsync();
        return rate;
    }
}
