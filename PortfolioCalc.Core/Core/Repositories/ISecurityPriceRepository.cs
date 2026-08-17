using PortfolioCalc.Core.Domain;

namespace PortfolioCalc.Core.Repositories;

public interface ISecurityPriceRepository
{
    Task<SecurityPrice?> GetAsync(int securityId, DateOnly date);
    Task<IReadOnlyList<SecurityPrice>> GetRangeAsync(int securityId, DateOnly from, DateOnly to);
    Task<SecurityPrice> AddAsync(SecurityPrice price);
}
