namespace PortfolioCalc.Core.Domain;

/// <summary>A security held in a specific account, so the same security across
/// different accounts is tracked separately.</summary>
public class Position
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public Account? Account { get; set; }
    public int SecurityId { get; set; }
    public Security? Security { get; set; }
}
