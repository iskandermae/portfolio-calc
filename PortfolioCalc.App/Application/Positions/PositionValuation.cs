namespace PortfolioCalc.App.Application.Positions;

/// <summary>One currently-held position's valuation, as produced by
/// <see cref="PositionValuationService"/>. <see cref="IsResolved"/> is false whenever the
/// security's price or its conversion to base currency couldn't be resolved (e.g.
/// pending-validation, unsupported security, network error) — per doc/stories/09-portfolio-value-report.md
/// AC #4, an unresolved position is flagged and excluded from the grand total rather than
/// silently valued at a missing/unvalidated figure.</summary>
public sealed record PositionValuation(
    int PositionId,
    string AccountName,
    string SecuritySymbol,
    string SecurityCurrency,
    decimal Quantity,
    decimal? Price,
    decimal? ValueInSecurityCurrency,
    decimal? ValueInBaseCurrency,
    bool IsResolved);
