namespace PortfolioCalc.App.Application.Positions;

/// <summary>One position's additional analytics figures for the Position TAB — see
/// doc/stories/11-position-performance-report.md. All amounts are in base currency.
/// <see cref="CashFlowResult"/> includes the position's current market value (for whatever
/// quantity is still held), not just its realized transaction cash flows — see
/// <see cref="PositionPerformanceService.GetPerformanceAsync"/>. <see cref="IsFullyResolved"/>
/// is false whenever at least one linked transaction's amount, fee/tax, inflation adjustment,
/// or the current value itself couldn't be resolved — that contribution is simply excluded
/// from the totals (a gap, not a crash), and the figures are flagged as incomplete rather than
/// silently understated.</summary>
public sealed record PositionPerformanceFigures(
    decimal NetInvested,
    decimal TotalDividends,
    decimal TotalFeesAndTaxes,
    decimal CashFlowResult,
    bool IsFullyResolved);
