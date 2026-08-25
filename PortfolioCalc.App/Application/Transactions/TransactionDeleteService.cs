using PortfolioCalc.Core.Repositories;

namespace PortfolioCalc.App.Application.Transactions;

public class TransactionDeleteService(
    ISecurityTransactionRepository securityTransactionRepository,
    ICashTransactionRepository cashTransactionRepository)
{
    public async Task DeleteSecurityTransactionAsync(int transactionId)
    {
        await securityTransactionRepository.DeleteAsync(transactionId);
    }

    public async Task DeleteCashTransactionAsync(int transactionId)
    {
        await cashTransactionRepository.DeleteAsync(transactionId);
    }
}