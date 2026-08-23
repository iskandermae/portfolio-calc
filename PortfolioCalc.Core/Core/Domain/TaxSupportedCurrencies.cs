namespace PortfolioCalc.Core.Domain;

/// <summary>Currencies offered by the tax-estimation report's own base-currency picker —
/// deliberately separate from <see cref="SupportedCurrencies"/> (see
/// doc/stories/12-tax-estimation-report.md). UAH is covered by a dedicated NBU-backed
/// <c>IFxRateProvider</c>, not Frankfurter.</summary>
public static class TaxSupportedCurrencies
{
    public static readonly IReadOnlyList<string> Codes = new[] { "USD", "EUR", "GBP", "UAH" };

    public const string DefaultCode = "GBP";
}
