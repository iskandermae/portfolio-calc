using Microsoft.EntityFrameworkCore;
using PortfolioCalc.Core.Domain;
using PortfolioCalc.Core.Repositories;

namespace PortfolioCalc.Core.Data.Repositories;

public class AppSettingsRepository(PortfolioDbContext context) : IAppSettingsRepository
{
    public Task<AppSettings?> GetAsync() =>
        context.AppSettings.FirstOrDefaultAsync(s => s.Id == AppSettings.SingletonId);

    public async Task SaveAsync(AppSettings settings)
    {
        var existing = await context.AppSettings.FindAsync(AppSettings.SingletonId);
        if (existing is null)
        {
            settings.Id = AppSettings.SingletonId;
            context.AppSettings.Add(settings);
        }
        else
        {
            existing.BaseCurrency = settings.BaseCurrency;
            existing.TaxBaseCurrency = settings.TaxBaseCurrency;
        }

        await context.SaveChangesAsync();
    }
}
