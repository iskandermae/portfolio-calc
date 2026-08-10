namespace PortfolioCalc.Core.Domain;

public class Security
{
    public int Id { get; set; }
    public required string Ticker { get; set; }
    public required string Name { get; set; }
    public required string Currency { get; set; }
}
