namespace PortfolioCalc.App.Application.Positions;

/// <summary>One resolved point of a <see cref="PositionValueChartService"/> series — see
/// doc/stories/10-position-value-chart-report.md. A sample date with no resolvable price/FX
/// rate/inflation rate simply produces no point (AC3 — no crash on a data gap), rather than a
/// null placeholder.</summary>
public sealed record ChartPoint(DateOnly Date, decimal ValueInBaseCurrency);

/// <summary>Result of <see cref="PositionValueChartService.BuildChartAsync"/>: the selected
/// security's value series and the CSPX.L comparison series, both in <see
/// cref="BaseCurrency"/> and both inflation-adjusted the same way when requested.</summary>
public sealed record PositionValueChartResult(
    IReadOnlyList<ChartPoint> PrimarySeries,
    string PrimarySymbol,
    IReadOnlyList<ChartPoint> ComparisonSeries,
    string ComparisonSymbol,
    string BaseCurrency);
