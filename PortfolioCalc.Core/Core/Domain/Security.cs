namespace PortfolioCalc.Core.Domain;

public class Security
{
    public int Id { get; set; }
    /// <summary>The security's identity key — assumed globally unique. See doc/decisions.md.</summary>
    public required string Symbol { get; set; }
    public required string Name { get; set; }
    public required string Currency { get; set; }
}
