using PortfolioCalc.Core.Domain;

namespace PortfolioCalc.Core.Repositories;

public interface ISecurityRepository
{
    Task<Security> AddAsync(Security security);
    Task<Security?> GetByIdAsync(int id);
    Task DeleteAsync(int id);
}
