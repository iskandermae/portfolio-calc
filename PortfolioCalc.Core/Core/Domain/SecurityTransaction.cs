namespace PortfolioCalc.Core.Domain;

/// <summary>A transaction against a position: buy/sell/dividend today, extensible to
/// coupons and other types as new needs arise.</summary>
public class SecurityTransaction
{
    public int Id { get; set; }
    public int PositionId { get; set; }
    public Position? Position { get; set; }
    public SecurityTransactionType Type { get; set; }
    public DateOnly Date { get; set; }
    public decimal? Quantity { get; set; }
    public decimal Amount { get; set; }
    public required string Currency { get; set; }
    public decimal? FeeAmount { get; set; }
    public string? FeeCurrency { get; set; }
    public string? Note { get; set; }

    public void Validate()
    {
        if (Amount <= 0)
            throw new InvalidOperationException("Amount must be positive.");
        if (string.IsNullOrWhiteSpace(Currency))
            throw new InvalidOperationException("Currency is required.");
        if (FeeAmount is < 0)
            throw new InvalidOperationException("FeeAmount cannot be negative.");
        if (FeeAmount.HasValue != !string.IsNullOrWhiteSpace(FeeCurrency))
            throw new InvalidOperationException("FeeAmount and FeeCurrency must be set together.");

        var quantityRequired = Type is SecurityTransactionType.Buy or SecurityTransactionType.Sell;
        if (quantityRequired && Quantity is not > 0)
            throw new InvalidOperationException($"Quantity is required and must be positive for {Type} transactions.");
        if (!quantityRequired && Quantity is not null)
            throw new InvalidOperationException($"Quantity must not be set for {Type} transactions.");
    }
}
