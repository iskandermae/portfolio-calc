namespace PortfolioCalc.Core.Domain;

public class Security
{
    public int Id { get; set; }
    /// <summary>The security's identity key — assumed globally unique. See doc/decisions.md.</summary>
    public required string Symbol { get; set; }
    public required string Name { get; set; }
    public required string Currency { get; set; }

    /// <summary>The broker's raw listing-exchange code (e.g. IBKR's "LSEETF", "IBIS",
    /// "ARCA"), stored as-is — null/empty when the import row didn't report one (e.g.
    /// pre-existing rows imported before this field existed). Used to resolve an
    /// exchange-specific price-provider symbol (see the "ExchangeYahooSuffix"
    /// vocabulary, doc/decisions.md) instead of guessing from Symbol/Currency alone.</summary>
    public string? Exchange { get; set; }
}
