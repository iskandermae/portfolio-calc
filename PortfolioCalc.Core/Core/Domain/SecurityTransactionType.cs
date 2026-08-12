namespace PortfolioCalc.Core.Domain;

public enum SecurityTransactionType
{
    Buy,
    Sell,
    Dividend,
    /// <summary>A standalone withholding-tax event with no dividend/payment-in-lieu row
    /// in the same (Security, Date) group to attach to (e.g. a tax-rate correction on a
    /// prior period). <see cref="SecurityTransaction.Amount"/> is the signed tax total,
    /// typically negative. See doc/decisions.md.</summary>
    Tax,
    /// <summary>An in-kind transfer of shares into an account (no cash impact) — e.g.
    /// IBKR's `&lt;Transfer type="FOP" direction="IN"&gt;`. See doc/decisions.md.</summary>
    TransferIn,
}
