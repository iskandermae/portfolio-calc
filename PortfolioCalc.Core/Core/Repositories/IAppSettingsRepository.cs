using PortfolioCalc.Core.Domain;

namespace PortfolioCalc.Core.Repositories;

public interface IAppSettingsRepository
{
    /// <summary>Returns the current settings row, or null if none has been saved yet.</summary>
    Task<AppSettings?> GetAsync();

    /// <summary>Upserts the single settings row (no history) — see
    /// doc/stories/05-base-currency-setting.md.</summary>
    Task SaveAsync(AppSettings settings);
}
