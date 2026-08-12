using PortfolioCalc.Core.Domain;

namespace PortfolioCalc.Core.Repositories;

public interface IAccountRepository
{
    Task<Account> AddAsync(Account account);
    Task<Account?> GetByIdAsync(int id);
    /// <summary>Looks up an account by its exact name — used by the IBKR import to
    /// match/auto-create an <see cref="Account"/> from the export's account
    /// alias/id (see doc/decisions.md).</summary>
    Task<Account?> GetByNameAsync(string name);
    Task DeleteAsync(int id);
}
