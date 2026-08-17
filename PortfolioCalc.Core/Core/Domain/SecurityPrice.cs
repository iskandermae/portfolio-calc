namespace PortfolioCalc.Core.Domain;

/// <summary>A cached price for one security on one date, in the security's trading
/// currency — see doc/stories/04-store-reuse-prices-rates.md.</summary>
public class SecurityPrice
{
    public int Id { get; set; }
    public int SecurityId { get; set; }
    public Security Security { get; set; } = null!;
    public DateOnly Date { get; set; }
    public decimal Price { get; set; }
}
