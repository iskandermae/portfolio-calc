using PortfolioCalc.Core.Domain;

namespace PortfolioCalc.Core.Tests.Domain;

public class SecurityTransactionTests
{
    private static SecurityTransaction ValidBuy() => new()
    {
        PositionId = 1,
        Type = SecurityTransactionType.Buy,
        Date = new DateOnly(2026, 1, 1),
        Quantity = 10m,
        Amount = -1000m,
        Currency = "USD",
    };

    private static SecurityTransaction ValidDividend() => new()
    {
        PositionId = 1,
        Type = SecurityTransactionType.Dividend,
        Date = new DateOnly(2026, 1, 1),
        Amount = 50m,
        Currency = "USD",
    };

    [Fact]
    public void Validate_accepts_a_well_formed_buy()
    {
        ValidBuy().Validate();
    }

    [Fact]
    public void Validate_accepts_a_well_formed_dividend_without_quantity()
    {
        ValidDividend().Validate();
    }

    [Theory]
    [InlineData(SecurityTransactionType.Buy)]
    [InlineData(SecurityTransactionType.Sell)]
    public void Validate_requires_positive_quantity_for_trades(SecurityTransactionType type)
    {
        var tx = ValidBuy();
        tx.Type = type;
        tx.Amount = type == SecurityTransactionType.Buy ? -1000m : 1000m;
        tx.Quantity = null;
        Assert.Throws<InvalidOperationException>(tx.Validate);
    }

    [Fact]
    public void Validate_rejects_a_positive_buy_amount()
    {
        var tx = ValidBuy();
        tx.Amount = 1000m;
        Assert.Throws<InvalidOperationException>(tx.Validate);
    }

    [Fact]
    public void Validate_rejects_a_negative_sell_amount()
    {
        var tx = ValidBuy();
        tx.Type = SecurityTransactionType.Sell;
        tx.Amount = -1000m;
        Assert.Throws<InvalidOperationException>(tx.Validate);
    }

    [Fact]
    public void Validate_rejects_quantity_on_a_dividend()
    {
        var tx = ValidDividend();
        tx.Quantity = 10m;
        Assert.Throws<InvalidOperationException>(tx.Validate);
    }

    [Fact]
    public void Validate_accepts_a_negative_tax_amount()
    {
        var tx = ValidDividend();
        tx.Type = SecurityTransactionType.Tax;
        tx.Amount = -5m;
        tx.Validate();
    }

    [Fact]
    public void Validate_rejects_a_zero_tax_amount()
    {
        var tx = ValidDividend();
        tx.Type = SecurityTransactionType.Tax;
        tx.Amount = 0m;
        Assert.Throws<InvalidOperationException>(tx.Validate);
    }

    [Fact]
    public void Validate_accepts_a_transfer_in_with_zero_amount_and_positive_quantity()
    {
        var tx = ValidDividend();
        tx.Type = SecurityTransactionType.TransferIn;
        tx.Amount = 0m;
        tx.Quantity = 35m;
        tx.Validate();
    }

    [Fact]
    public void Validate_rejects_a_transfer_in_with_a_nonzero_amount()
    {
        var tx = ValidDividend();
        tx.Type = SecurityTransactionType.TransferIn;
        tx.Amount = 10m;
        tx.Quantity = 35m;
        Assert.Throws<InvalidOperationException>(tx.Validate);
    }

    [Fact]
    public void Validate_rejects_a_transfer_in_without_quantity()
    {
        var tx = ValidDividend();
        tx.Type = SecurityTransactionType.TransferIn;
        tx.Amount = 0m;
        Assert.Throws<InvalidOperationException>(tx.Validate);
    }

    [Fact]
    public void Validate_rejects_non_positive_amount()
    {
        var tx = ValidBuy();
        tx.Amount = 0m;
        Assert.Throws<InvalidOperationException>(tx.Validate);
    }

    [Fact]
    public void Validate_accepts_fee_with_a_different_currency_than_the_trade()
    {
        var tx = ValidBuy();
        tx.FeeAmount = -2m;
        tx.FeeCurrency = "EUR";
        tx.Validate();
    }

    [Fact]
    public void Validate_rejects_fee_amount_without_fee_currency()
    {
        var tx = ValidBuy();
        tx.FeeAmount = -2m;
        Assert.Throws<InvalidOperationException>(tx.Validate);
    }

    [Fact]
    public void Validate_rejects_a_positive_fee_amount()
    {
        var tx = ValidBuy();
        tx.FeeAmount = 2m;
        tx.FeeCurrency = "GBP";
        Assert.Throws<InvalidOperationException>(tx.Validate);
    }

    [Fact]
    public void Validate_accepts_a_negative_tax_amount_on_a_dividend()
    {
        var tx = ValidDividend();
        tx.TaxAmount = -5m;
        tx.Validate();
    }

    [Fact]
    public void Validate_rejects_a_tax_amount_on_a_non_dividend_transaction()
    {
        var tx = ValidBuy();
        tx.TaxAmount = -5m;
        Assert.Throws<InvalidOperationException>(tx.Validate);
    }
}
