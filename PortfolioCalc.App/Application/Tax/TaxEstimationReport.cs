namespace PortfolioCalc.App.Application.Tax;

public sealed record TaxEstimationReport(
    IReadOnlyList<TaxEstimationRow> Rows, decimal TotalGainInBaseCurrency, string BaseCurrency);
