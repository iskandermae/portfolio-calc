namespace PortfolioCalc.Core.Domain;

/// <summary>A cached FX rate for one currency pair on one date — see
/// doc/stories/04-store-reuse-prices-rates.md.</summary>
public class FxRate
{
    public int Id { get; set; }
    public required string FromCurrency { get; set; }
    public required string ToCurrency { get; set; }
    public DateOnly Date { get; set; }
    public decimal Rate { get; set; }
}
