namespace PortfolioCalc.App.Application.Transactions;

/// <summary>A single Buy transaction's additional analytics figures for the Transaction TAB —
/// see doc/stories/11-position-performance-report.md. Null fields mean the figure couldn't be
/// resolved (e.g. no current price, no FX rate, no inflation rate) rather than zero.</summary>
public sealed record BuyTransactionPerformance(decimal? Cagr, decimal? CashFlowResult);
