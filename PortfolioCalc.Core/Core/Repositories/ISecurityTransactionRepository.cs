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
}
