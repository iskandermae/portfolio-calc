namespace PortfolioCalc.Core.Domain;

public enum CashTransactionType
{
    Deposit,
    Withdrawal,
    /// <summary>Broker-paid interest on cash balances (IBKR's "Broker Interest
    /// Received") — account-level income, not tied to any Position, and not a Deposit
    /// (which is an external cash movement, not income). See doc/decisions.md.</summary>
    Interest,
}
