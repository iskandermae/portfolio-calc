using PortfolioCalc.Core.Domain;

namespace PortfolioCalc.Core.Repositories;

public interface IFxRateRepository
{
    Task<FxRate?> GetAsync(string fromCurrency, string toCurrency, DateOnly date);
    Task<IReadOnlyList<FxRate>> GetRangeAsync(string fromCurrency, string toCurrency, DateOnly from, DateOnly to);

    /// <summary>Every stored rate across all currency pairs, for the Vocabularies
    /// page's read-only "FX Rates" sub-tab.</summary>
    Task<IReadOnlyList<FxRate>> GetAllAsync();

    Task<FxRate> AddAsync(FxRate rate);

    /// <summary>All rates currently awaiting manual review — see
    /// doc/stories/07-price-rate-quality-validation.md.</summary>
    Task<IReadOnlyList<FxRate>> GetPendingAsync();

    /// <summary>Marks a stored rate's review status, optionally correcting its value (e.g.
    /// when a user fixes a rate they're marking valid).</summary>
    Task UpdateStatusAsync(int id, ValidationStatus status, decimal? correctedRate = null);
}
