namespace PortfolioCalc.Core.Domain;

/// <summary>A pure account-level cash movement (top-up, withdrawal), not tied to a
/// position. Dividends/coupons/fees on a security are recorded as <see cref="SecurityTransaction"/>.</summary>
public class CashTransaction
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public Account? Account { get; set; }
    public CashTransactionType Type { get; set; }
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public required string Currency { get; set; }
    public decimal? FeeAmount { get; set; }
    public string? FeeCurrency { get; set; }
    public string? Note { get; set; }

    public void Validate()
    {
        // Sign convention (see doc/decisions.md): incoming amounts (Deposit, Interest) are
        // positive; outgoing amounts (Withdrawal) are negative.
        if (Type is CashTransactionType.Withdrawal)
        {
            if (Amount >= 0)
                throw new InvalidOperationException("Amount must be negative for Withdrawal transactions.");
        }
        else if (Amount <= 0)
        {
            throw new InvalidOperationException("Amount must be positive.");
        }

        if (string.IsNullOrWhiteSpace(Currency))
            throw new InvalidOperationException("Currency is required.");
        if (FeeAmount is > 0)
            throw new InvalidOperationException("FeeAmount cannot be positive.");
        if (FeeAmount.HasValue != !string.IsNullOrWhiteSpace(FeeCurrency))
            throw new InvalidOperationException("FeeAmount and FeeCurrency must be set together.");
    }
}
