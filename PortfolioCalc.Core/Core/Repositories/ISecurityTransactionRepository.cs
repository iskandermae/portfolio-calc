using PortfolioCalc.Core.Domain;

namespace PortfolioCalc.Core.Repositories;

public interface ISecurityTransactionRepository
{
    Task<SecurityTransaction> AddAsync(SecurityTransaction transaction);
    Task<SecurityTransaction?> GetByIdAsync(int id);
    Task UpdateAsync(SecurityTransaction transaction);
    Task DeleteAsync(int id);
    Task<IReadOnlyList<SecurityTransaction>> GetByDateRangeAsync(DateOnly from, DateOnly to);
    Task<IReadOnlyList<SecurityTransaction>> GetBySecurityAsync(int securityId);
    /// <summary>Transactions for one Position — used by the IBKR import to dedup a
    /// mapped transaction against what's already stored (Position + Amount + Date +
    /// Currency; see doc/decisions.md) before inserting it.</summary>
    Task<IReadOnlyList<SecurityTransaction>> GetByPositionAsync(int positionId);
}
