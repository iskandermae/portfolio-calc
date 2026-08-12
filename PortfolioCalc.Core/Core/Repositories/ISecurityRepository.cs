using PortfolioCalc.Core.Domain;

namespace PortfolioCalc.Core.Repositories;

public interface ISecurityRepository
{
    Task<Security> AddAsync(Security security);
    Task<Security?> GetByIdAsync(int id);
    /// <summary>Looks up a security by its identity key — Symbol + Currency (see
    /// doc/decisions.md) — used by the IBKR import to match/auto-create a
    /// <see cref="Security"/>.</summary>
    Task<Security?> GetBySymbolAndCurrencyAsync(string symbol, string currency);
    Task DeleteAsync(int id);
}
