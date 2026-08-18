namespace PortfolioCalc.Core.Domain;

/// <summary>Classifies a transaction type for the Transactions report's default filter
/// (story 08, enhancement) — a single "show Secondary too" toggle instead of a per-type
/// checklist in the Gui. See doc/decisions.md.</summary>
public enum TransactionCategory
{
    /// <summary>The core economic events shown by default: buying/selling/holding a
    /// security, and moving cash in or out of an account.</summary>
    Primary,

    /// <summary>Smaller, more numerous cash-flow noise, hidden by default: dividends,
    /// withholding tax, broker interest.</summary>
    Secondary,
}

/// <summary>Maps every <see cref="CashTransactionType"/>/<see cref="SecurityTransactionType"/>
/// to a <see cref="TransactionCategory"/>. Every current type is classified — a new type
/// must be added here too, or it throws rather than silently vanishing from both views.</summary>
public static class TransactionCategoryClassifier
{
    public static TransactionCategory Classify(CashTransactionType type) => type switch
    {
        CashTransactionType.Deposit => TransactionCategory.Primary,
        CashTransactionType.Withdrawal => TransactionCategory.Primary,
        CashTransactionType.Interest => TransactionCategory.Secondary,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unclassified CashTransactionType."),
    };

    public static TransactionCategory Classify(SecurityTransactionType type) => type switch
    {
        SecurityTransactionType.Buy => TransactionCategory.Primary,
        SecurityTransactionType.Sell => TransactionCategory.Primary,
        SecurityTransactionType.TransferIn => TransactionCategory.Primary,
        SecurityTransactionType.Dividend => TransactionCategory.Secondary,
        SecurityTransactionType.Tax => TransactionCategory.Secondary,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unclassified SecurityTransactionType."),
    };
}
