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
}
