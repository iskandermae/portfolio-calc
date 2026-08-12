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
    /// <summary>Withholding tax matched to this Dividend's (Security, Date) group, in the
    /// same <see cref="Currency"/> as the dividend. Kept separate from <see cref="FeeAmount"/>
    /// (a broker commission) — the two are unrelated costs. Only set for <see
    /// cref="SecurityTransactionType.Dividend"/>; a standalone tax with no dividend to
    /// attach to is its own <see cref="SecurityTransactionType.Tax"/> transaction instead.
    /// See doc/decisions.md.</summary>
    public decimal? TaxAmount { get; set; }
    public string? Note { get; set; }

    public void Validate()
    {
        // Sign convention (see doc/decisions.md): incoming/profit amounts are positive,
        // outgoing/cost amounts are negative. Buy = cash out (negative); Sell/Dividend =
        // cash in (positive); Tax is a signed total (no magnitude constraint, since it
        // can flip sign under a rate correction); TransferIn has no cash amount at all.
        switch (Type)
        {
            case SecurityTransactionType.Buy:
                if (Amount >= 0)
                    throw new InvalidOperationException("Amount must be negative for Buy transactions.");
                break;
            case SecurityTransactionType.Sell:
            case SecurityTransactionType.Dividend:
                if (Amount <= 0)
                    throw new InvalidOperationException("Amount must be positive.");
                break;
            case SecurityTransactionType.Tax:
                if (Amount == 0)
                    throw new InvalidOperationException("Amount must be nonzero for Tax transactions.");
                break;
            case SecurityTransactionType.TransferIn:
                if (Amount != 0)
                    throw new InvalidOperationException("Amount must be zero for TransferIn transactions.");
                break;
        }

        if (string.IsNullOrWhiteSpace(Currency))
            throw new InvalidOperationException("Currency is required.");
        if (FeeAmount is > 0)
            throw new InvalidOperationException("FeeAmount cannot be positive.");
        if (FeeAmount.HasValue != !string.IsNullOrWhiteSpace(FeeCurrency))
            throw new InvalidOperationException("FeeAmount and FeeCurrency must be set together.");
        if (TaxAmount is not null && Type != SecurityTransactionType.Dividend)
            throw new InvalidOperationException("TaxAmount is only valid on Dividend transactions.");

        var quantityRequired = Type is SecurityTransactionType.Buy or SecurityTransactionType.Sell or SecurityTransactionType.TransferIn;
        if (quantityRequired && Quantity is not > 0)
            throw new InvalidOperationException($"Quantity is required and must be positive for {Type} transactions.");
        if (!quantityRequired && Quantity is not null)
            throw new InvalidOperationException($"Quantity must not be set for {Type} transactions.");
    }
}
