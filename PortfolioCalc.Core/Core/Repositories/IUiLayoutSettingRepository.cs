using PortfolioCalc.Core.Domain;

namespace PortfolioCalc.Core.Repositories;

public interface IUiLayoutSettingRepository
{
    /// <summary>Returns the saved layout for the given screen, or null if none has
    /// been saved yet.</summary>
    Task<UiLayoutSetting?> GetAsync(string screenKey);

    /// <summary>Upserts the layout row for the given screen (no history).</summary>
    Task SaveAsync(string screenKey, string layoutJson);
}
