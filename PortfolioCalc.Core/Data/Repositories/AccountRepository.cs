using Microsoft.EntityFrameworkCore;
using PortfolioCalc.Core.Domain;
using PortfolioCalc.Core.Repositories;

namespace PortfolioCalc.Core.Data.Repositories;

public class AccountRepository(PortfolioDbContext context) : IAccountRepository
{
    public async Task<Account> AddAsync(Account account)
    {
        context.Accounts.Add(account);
        await context.SaveChangesAsync();
        return account;
    }

    public Task<Account?> GetByIdAsync(int id) =>
        context.Accounts.FirstOrDefaultAsync(a => a.Id == id);

    public Task<Account?> GetByNameAsync(string name) =>
        context.Accounts.FirstOrDefaultAsync(a => a.Name == name);

    public async Task DeleteAsync(int id)
    {
        var entity = await context.Accounts.FindAsync(id);
        if (entity is not null)
        {
            context.Accounts.Remove(entity);
            await context.SaveChangesAsync();
        }
    }
}
