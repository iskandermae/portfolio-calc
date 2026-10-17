namespace PortfolioCalc.App.Application.Import.T212;

/// <summary>Trading 212's own row-type/value strings (CSV column values), named once here
/// and reused everywhere they're matched instead of being repeated as literals across the
/// mapping logic.</summary>
internal static class T212Constants
{
    public const string ActionDeposit = "Deposit";
    public const string ActionInterestOnCash = "Interest on cash";
    public const string ActionDividend = "Dividend (Dividend)";
    public const string ActionMarketBuy = "Market buy";
    public const string ActionLimitBuy = "Limit buy";
    public const string ActionMarketSell = "Market sell";
    public const string ActionLimitSell = "Limit sell";

    public static readonly IReadOnlySet<string> BuyActions = new HashSet<string> { ActionMarketBuy, ActionLimitBuy };
    public static readonly IReadOnlySet<string> SellActions = new HashSet<string> { ActionMarketSell, ActionLimitSell };

    /// <summary>T212's currency code for GBP quoted in pence — same pence/pounds split
    /// Yahoo reports as "GBp" (see doc/decisions.md), just a different source's own code.</summary>
    public const string CurrencyGbx = "GBX";
    public const string CurrencyGbp = "GBP";

    /// <summary>Placeholder exchange code assigned to GBP-traded T212 securities, since the
    /// CSV has no listing-exchange column. The user maps it to ".L" in the
    /// "ExchangeYahooSuffix" vocabulary (see doc/decisions.md).</summary>
    public const string ExchangeGbpGuess = "MY_T212.L";
}
