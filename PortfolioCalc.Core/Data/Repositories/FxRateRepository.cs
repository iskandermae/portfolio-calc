using Microsoft.EntityFrameworkCore;
using PortfolioCalc.Core.Domain;
using PortfolioCalc.Core.Repositories;

namespace PortfolioCalc.Core.Data.Repositories;

public class FxRateRepository(PortfolioDbContext context) : IFxRateRepository
{
    public Task<FxRate?> GetAsync(string fromCurrency, string toCurrency, DateOnly date) =>
        context.FxRates.FirstOrDefaultAsync(r =>
            r.FromCurrency == fromCurrency && r.ToCurrency == toCurrency && r.Date == date);

    public async Task<IReadOnlyList<FxRate>> GetRangeAsync(
        string fromCurrency, string toCurrency, DateOnly from, DateOnly to) =>
        await context.FxRates
            .Where(r => r.FromCurrency == fromCurrency && r.ToCurrency == toCurrency
                        && r.Date >= from && r.Date <= to)
            .OrderBy(r => r.Date)
            .ToListAsync();

    public async Task<IReadOnlyList<FxRate>> GetAllAsync() =>
        await context.FxRates.OrderByDescending(r => r.Date).ToListAsync();

    public async Task<FxRate> AddAsync(FxRate rate)
    {
        context.FxRates.Add(rate);
        await context.SaveChangesAsync();
        return rate;
    }

    public async Task<IReadOnlyList<FxRate>> GetPendingAsync() =>
        await context.FxRates
            .Where(r => r.Status == ValidationStatus.PendingValidation)
            .OrderBy(r => r.Date)
            .ToListAsync();

    public async Task UpdateStatusAsync(int id, ValidationStatus status, decimal? correctedRate = null)
    {
        var rate = await context.FxRates.FindAsync(id)
            ?? throw new InvalidOperationException($"FxRate {id} not found.");
        rate.Status = status;
        if (correctedRate is not null)
            rate.Rate = correctedRate.Value;
        await context.SaveChangesAsync();
    }
}
