using PortfolioCalc.App.Application.Transactions;
using PortfolioCalc.Core.Domain;

namespace PortfolioCalc.App.Tests.Application.Transactions;

public class TransactionDetailsFormatterTests
{
    [Fact]
    public void Format_security_transaction_includes_transaction_position_security_and_account_fields()
    {
        var transaction = new SecurityTransaction
        {
            Id = 11,
            Type = SecurityTransactionType.Buy,
            Date = new DateOnly(2026, 2, 1),
            Quantity = 10m,
            Amount = -1000m,
            Currency = "USD",
            FeeAmount = -1.5m,
            FeeCurrency = "GBP",
            Note = "a note",
            Position = new Position
            {
                Id = 22,
                Account = new Account { Id = 33, Name = "test_account" },
                Security = new Security { Id = 44, Symbol = "TSTA", Name = "Test A", Currency = "USD", Exchange = "MY_T212.L" },
            },
        };

        var text = TransactionDetailsFormatter.Format(transaction);

        foreach (var expected in new[]
        {
            "Transaction Id: 11", "Type: Buy", "Date: 2026-02-01", "Quantity: 10", "Amount: -1000",
            "Currency: USD", "Fee amount: -1.5", "Fee currency: GBP", "Note: a note",
            "Position Id: 22", "Security Id: 44", "Symbol: TSTA", "Name: Test A", "Exchange: MY_T212.L",
            "Account Id: 33", "Account name: test_account",
        })
            Assert.Contains(expected, text);
    }

    [Fact]
    public void Format_security_transaction_shows_an_unset_exchange_as_empty()
    {
        var transaction = new SecurityTransaction
        {
            Type = SecurityTransactionType.Dividend,
            Currency = "USD",
            TaxAmount = -0.9m,
            Position = new Position
            {
                Account = new Account { Name = "a" },
                Security = new Security { Symbol = "S", Name = "S", Currency = "USD" },
            },
        };

        var text = TransactionDetailsFormatter.Format(transaction);

        Assert.Contains("Exchange: \n", text.Replace("\r\n", "\n"));
        Assert.Contains("Tax amount: -0.9", text);
    }

    [Fact]
    public void Format_cash_transaction_includes_transaction_and_account_fields()
    {
        var transaction = new CashTransaction
        {
            Id = 5,
            Type = CashTransactionType.Deposit,
            Date = new DateOnly(2026, 1, 10),
            Amount = 1000m,
            Currency = "GBP",
            Account = new Account { Id = 7, Name = "test_account" },
        };

        var text = TransactionDetailsFormatter.Format(transaction);

        Assert.Contains("Cash transaction", text);
        Assert.Contains("Transaction Id: 5", text);
        Assert.Contains("Type: Deposit", text);
        Assert.Contains("Account Id: 7", text);
        Assert.Contains("Account name: test_account", text);
    }
}
