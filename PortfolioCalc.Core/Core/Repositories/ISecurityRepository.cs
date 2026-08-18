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

    /// <summary>Persists changes to an already-tracked-or-reloaded Security (e.g.
    /// backfilling <see cref="Security.Exchange"/> on a pre-existing row — see
    /// doc/decisions.md).</summary>
    Task UpdateAsync(Security security);

    Task DeleteAsync(int id);
}
