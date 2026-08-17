namespace PortfolioCalc.Core.Domain;

/// <summary>A cached inflation rate for one base currency and one period — see
/// doc/stories/06-import-inflation-rates.md. <see cref="Period"/> is normalized to the
/// first day of the period (the 1st of January for the annual data this app currently
/// imports); a monthly source would normalize to the 1st of its month instead. Rate is a
/// percentage (e.g. 3.2 for 3.2%), matching how the source API reports it.</summary>
public class InflationRate
{
    public int Id { get; set; }
    public required string BaseCurrency { get; set; }
    public DateOnly Period { get; set; }
    public decimal Rate { get; set; }
}
