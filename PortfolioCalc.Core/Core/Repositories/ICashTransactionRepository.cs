using PortfolioCalc.Core.Domain;

namespace PortfolioCalc.Core.Repositories;

public interface ICashTransactionRepository
{
    Task<CashTransaction> AddAsync(CashTransaction transaction);
    Task<CashTransaction?> GetByIdAsync(int id);
    Task UpdateAsync(CashTransaction transaction);
    Task DeleteAsync(int id);
    Task<IReadOnlyList<CashTransaction>> GetByDateRangeAsync(DateOnly from, DateOnly to);
    Task<IReadOnlyList<CashTransaction>> GetByAccountAsync(int accountId);
    /// <summary>All cash transactions with Account loaded, for the transactions list
    /// report (doc/stories/08-transactions-report.md).</summary>
    Task<IReadOnlyList<CashTransaction>> GetAllAsync();
}
