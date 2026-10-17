using PortfolioCalc.Core.Domain;

namespace PortfolioCalc.Core.Repositories;

public interface IAccountRepository
{
    Task<Account> AddAsync(Account account);
    Task<Account?> GetByIdAsync(int id);
    /// <summary>All accounts, ordered by name — used by the Trading 212 import's account
    /// picker (see doc/decisions.md).</summary>
    Task<IReadOnlyList<Account>> GetAllAsync();
    /// <summary>Looks up an account by its exact name — used by the IBKR import to
    /// match/auto-create an <see cref="Account"/> from the export's account
    /// alias/id (see doc/decisions.md).</summary>
    Task<Account?> GetByNameAsync(string name);
    Task DeleteAsync(int id);
}
