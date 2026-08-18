using PortfolioCalc.Core.Domain;

namespace PortfolioCalc.Core.Repositories;

public interface ISecurityPriceRepository
{
    Task<SecurityPrice?> GetAsync(int securityId, DateOnly date);
    Task<IReadOnlyList<SecurityPrice>> GetRangeAsync(int securityId, DateOnly from, DateOnly to);
    Task<SecurityPrice> AddAsync(SecurityPrice price);

    /// <summary>All prices currently awaiting manual review — see
    /// doc/stories/07-price-rate-quality-validation.md.</summary>
    Task<IReadOnlyList<SecurityPrice>> GetPendingAsync();

    /// <summary>Marks a stored price's review status, optionally correcting its value
    /// (e.g. when a user fixes a price they're marking valid).</summary>
    Task UpdateStatusAsync(int id, ValidationStatus status, decimal? correctedPrice = null);
}
