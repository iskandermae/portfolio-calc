namespace PortfolioCalc.Core.Domain;

/// <summary>The known <see cref="VocabularyEntry.VocabularyType"/> values. New
/// vocabularies are added here as a new constant — no schema change needed, since
/// <see cref="VocabularyEntry"/> is a single generic table. See doc/decisions.md.</summary>
public static class VocabularyTypes
{
    /// <summary>Maps an IBKR listing-exchange code (e.g. "LSEETF") to the Yahoo Finance
    /// ticker suffix (e.g. ".L") used by <c>YahooFinanceSecurityPriceProvider</c>. A
    /// missing key, or an empty <see cref="VocabularyEntry.Value"/>, both mean "no
    /// suffix" — the provider falls back to the plain symbol.</summary>
    public const string ExchangeYahooSuffix = "ExchangeYahooSuffix";

    /// <summary>Manual inflation-rate overrides, keyed "{baseCurrency}:{year}" (e.g.
    /// "USD:2026"). Value is the rate as a percentage number, matching
    /// <see cref="InflationRate.Rate"/>'s convention (e.g. "5.2" for 5.2%) — used as a
    /// fallback when the real inflation data source has no published figure yet for a
    /// year (a very recent/current year is the common case). See
    /// <c>PortfolioCalc.Core.Data.Inflation.VocabularyOverrideInflationRateProvider</c> and
    /// doc/decisions.md.</summary>
    public const string InflationRateOverride = "InflationRateOverride";
}
