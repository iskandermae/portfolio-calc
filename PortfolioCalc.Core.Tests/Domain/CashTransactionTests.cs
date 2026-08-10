using PortfolioCalc.Core.Domain;

namespace PortfolioCalc.Core.Tests.Domain;

public class CashTransactionTests
{
    private static CashTransaction ValidDeposit() => new()
    {
        AccountId = 1,
        Type = CashTransactionType.Deposit,
        Date = new DateOnly(2026, 1, 1),
        Amount = 1000m,
        Currency = "USD",
    };

    [Fact]
    public void Validate_accepts_a_well_formed_transaction()
    {
        var tx = ValidDeposit();
        tx.Validate();
    }

    [Fact]
    public void Validate_accepts_a_transaction_with_matching_fee_fields()
    {
        var tx = ValidDeposit();
        tx.FeeAmount = 5m;
        tx.FeeCurrency = "USD";
        tx.Validate();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Validate_rejects_non_positive_amount(decimal amount)
    {
        var tx = ValidDeposit();
        tx.Amount = amount;
        Assert.Throws<InvalidOperationException>(tx.Validate);
    }

    [Fact]
    public void Validate_rejects_missing_currency()
    {
        var tx = ValidDeposit();
        tx.Currency = "";
        Assert.Throws<InvalidOperationException>(tx.Validate);
    }

    [Fact]
    public void Validate_rejects_fee_amount_without_fee_currency()
    {
        var tx = ValidDeposit();
        tx.FeeAmount = 5m;
        Assert.Throws<InvalidOperationException>(tx.Validate);
    }

    [Fact]
    public void Validate_rejects_fee_currency_without_fee_amount()
    {
        var tx = ValidDeposit();
        tx.FeeCurrency = "USD";
        Assert.Throws<InvalidOperationException>(tx.Validate);
    }

    [Fact]
    public void Validate_rejects_negative_fee_amount()
    {
        var tx = ValidDeposit();
        tx.FeeAmount = -1m;
        tx.FeeCurrency = "USD";
        Assert.Throws<InvalidOperationException>(tx.Validate);
    }
}
