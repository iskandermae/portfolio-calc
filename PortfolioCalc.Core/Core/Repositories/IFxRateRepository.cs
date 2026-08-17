using PortfolioCalc.Core.Domain;

namespace PortfolioCalc.Core.Repositories;

public interface IFxRateRepository
{
    Task<FxRate?> GetAsync(string fromCurrency, string toCurrency, DateOnly date);
    Task<IReadOnlyList<FxRate>> GetRangeAsync(string fromCurrency, string toCurrency, DateOnly from, DateOnly to);
    Task<FxRate> AddAsync(FxRate rate);
}
