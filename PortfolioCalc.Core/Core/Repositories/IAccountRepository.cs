using PortfolioCalc.Core.Domain;

namespace PortfolioCalc.Core.Repositories;

public interface IAccountRepository
{
    Task<Account> AddAsync(Account account);
    Task<Account?> GetByIdAsync(int id);
    Task DeleteAsync(int id);
}
