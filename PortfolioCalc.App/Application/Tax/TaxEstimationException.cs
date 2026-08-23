namespace PortfolioCalc.App.Application.Tax;

/// <summary>Thrown by <see cref="TaxEstimationService"/> when a historical price or FX rate
/// (a buy date, an actual sell date, or a TransferIn date) can't be resolved — per an explicit
/// business decision, the whole report stops with an error rather than excluding that one
/// figure (see doc/stories/12-tax-estimation-report.md).</summary>
public sealed class TaxEstimationException(string message) : Exception(message);
