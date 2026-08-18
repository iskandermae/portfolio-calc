namespace PortfolioCalc.App.Application.Positions;

/// <summary>Result of <see cref="PositionValuationService.GetCurrentValueAsync"/> —
/// see doc/stories/09-portfolio-value-report.md. <see cref="GrandTotalInBaseCurrency"/> sums
/// only the positions where <see cref="PositionValuation.IsResolved"/> is true.</summary>
public sealed record PortfolioValueReport(
    IReadOnlyList<PositionValuation> Positions,
    decimal GrandTotalInBaseCurrency,
    string BaseCurrency);
