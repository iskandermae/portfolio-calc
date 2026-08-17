using PortfolioCalc.Core.Domain;

namespace PortfolioCalc.Core.Repositories;

public interface IInflationRateRepository
{
    Task<InflationRate?> GetAsync(string baseCurrency, DateOnly period);
    Task<IReadOnlyList<InflationRate>> GetRangeAsync(string baseCurrency, DateOnly from, DateOnly to);
    Task<InflationRate> AddAsync(InflationRate rate);
}
