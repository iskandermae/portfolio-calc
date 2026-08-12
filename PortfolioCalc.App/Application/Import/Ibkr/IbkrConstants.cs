using System.Text.RegularExpressions;

namespace PortfolioCalc.App.Application.Import.Ibkr;

/// <summary>IBKR's own row-type/value strings (Flex Query XML attributes), named once
/// here and reused everywhere they're matched instead of being repeated as literals
/// across the mapping logic.</summary>
internal static class IbkrConstants
{
    public const string CashTypeDividends = "Dividends";
    public const string CashTypePaymentInLieuOfDividends = "Payment In Lieu Of Dividends";
    public const string CashTypeWithholdingTax = "Withholding Tax";
    public const string CashTypeDepositsWithdrawals = "Deposits/Withdrawals";
    public const string CashTypeBrokerInterest = "Broker Interest Received";
    public const string TradeTransactionTypeExchTrade = "ExchTrade";
    public const string BuySellBuy = "BUY";
    public const string BuySellSell = "SELL";
    public const string TransferTypeFop = "FOP";
    public const string TransferDirectionIn = "IN";

    public static readonly IReadOnlySet<string> DividendGroupTypes =
        new HashSet<string> { CashTypeDividends, CashTypePaymentInLieuOfDividends, CashTypeWithholdingTax };

    /// <summary>IBKR's FX auto-conversion "trades" (e.g. symbol "EUR.GBP") that settle a
    /// trade placed in a different currency — not a real security trade.</summary>
    public static readonly Regex CurrencyPairSymbol = new(@"^[A-Z]{3}\.[A-Z]{3}$", RegexOptions.Compiled);
}
