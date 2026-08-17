namespace PortfolioCalc.Core.Domain;

/// <summary>Small, static list of currencies offered by the base-currency picker — not a
/// full ISO-4217 table (see doc/decisions.md). Extend only when a real need shows up.</summary>
public static class SupportedCurrencies
{
    public static readonly IReadOnlyList<string> Codes =
        new[] { "USD", "EUR", "GBP", "CHF", "JPY", "AUD", "CAD" };
}
