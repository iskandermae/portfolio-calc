namespace PortfolioCalc.App.Application.Tax;

/// <summary>One position's (Account × Security) row in the tax-estimation report — see
/// doc/stories/12-tax-estimation-report.md. Actual sells (within the tax year) and the
/// entered proposed sell for this position are summed together; the same security held in a
/// different account gets its own separate row (see doc/decisions.md). <see cref="BuyFxRate"/>/
/// <see cref="SellFxRate"/> are blended (base-currency total ÷ security-currency total) since
/// average cost and multiple sells can each span more than one underlying date/rate.</summary>
public sealed record TaxEstimationRow(
    string AccountName,
    string SecuritySymbol,
    string SecurityCurrency,
    decimal QuantitySold,
    decimal AverageBuyCostInSecurityCurrency,
    decimal SellAmountInSecurityCurrency,
    decimal BuyFxRate,
    decimal SellFxRate,
    decimal AverageBuyCostInBaseCurrency,
    decimal SellAmountInBaseCurrency,
    decimal GainInBaseCurrency);
