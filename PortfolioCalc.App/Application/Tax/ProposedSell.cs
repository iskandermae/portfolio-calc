namespace PortfolioCalc.App.Application.Tax;

/// <summary>One entered "sell today" row — see doc/stories/12-tax-estimation-report.md.
/// Scoped to a specific <see cref="PositionId"/> (Account × Security), not just a security —
/// the same security held in two accounts has two independent cost bases and holdings (see
/// doc/decisions.md). <see cref="Quantity"/> must not exceed that position's currently held
/// quantity.</summary>
public sealed record ProposedSell(int PositionId, decimal Quantity);
