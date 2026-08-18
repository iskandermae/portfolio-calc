using Microsoft.EntityFrameworkCore;
using PortfolioCalc.Core.Domain;
using PortfolioCalc.Core.Repositories;

namespace PortfolioCalc.Core.Data.Repositories;

public class UiLayoutSettingRepository(PortfolioDbContext context) : IUiLayoutSettingRepository
{
    public Task<UiLayoutSetting?> GetAsync(string screenKey) =>
        context.UiLayoutSettings.FirstOrDefaultAsync(s => s.ScreenKey == screenKey);

    public async Task SaveAsync(string screenKey, string layoutJson)
    {
        var existing = await context.UiLayoutSettings.FindAsync(screenKey);
        if (existing is null)
            context.UiLayoutSettings.Add(new UiLayoutSetting { ScreenKey = screenKey, LayoutJson = layoutJson });
        else
            existing.LayoutJson = layoutJson;

        await context.SaveChangesAsync();
    }
}
