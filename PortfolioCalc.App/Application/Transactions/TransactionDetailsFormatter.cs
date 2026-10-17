using System.Globalization;
using System.Text;
using PortfolioCalc.Core.Domain;

namespace PortfolioCalc.App.Application.Transactions;

/// <summary>Renders every stored field of a transaction, including its linked
/// Account/Position/Security, as plain "Label: value" lines for the Transactions page's
/// Details memo. Currencies are stored as plain codes (no currency entity), so only the
/// code is shown. Requires the navigation properties to be loaded
/// (<c>GetAllAsync</c> on the transaction repositories does this).</summary>
public static class TransactionDetailsFormatter
{
    public static string Format(SecurityTransaction transaction)
    {
        var position = transaction.Position!;
        var security = position.Security!;
        var account = position.Account!;

        var text = new StringBuilder();
        text.AppendLine("Security transaction");
        AppendTransactionFields(text, transaction.Id, transaction.Type.ToString(), transaction.Date,
            transaction.Amount, transaction.Currency, transaction.FeeAmount, transaction.FeeCurrency, transaction.Note,
            transaction.Quantity, transaction.TaxAmount);
        text.AppendLine();
        text.AppendLine("Position");
        Line(text, "Position Id", position.Id);
        text.AppendLine();
        text.AppendLine("Security");
        Line(text, "Security Id", security.Id);
        Line(text, "Symbol", security.Symbol);
        Line(text, "Name", security.Name);
        Line(text, "Currency", security.Currency);
        Line(text, "Exchange", security.Exchange);
        text.AppendLine();
        AppendAccount(text, account);
        return text.ToString();
    }

    public static string Format(CashTransaction transaction)
    {
        var text = new StringBuilder();
        text.AppendLine("Cash transaction");
        AppendTransactionFields(text, transaction.Id, transaction.Type.ToString(), transaction.Date,
            transaction.Amount, transaction.Currency, transaction.FeeAmount, transaction.FeeCurrency, transaction.Note);
        text.AppendLine();
        AppendAccount(text, transaction.Account!);
        return text.ToString();
    }

    private static void AppendTransactionFields(
        StringBuilder text, int id, string type, DateOnly date, decimal amount, string currency,
        decimal? feeAmount, string? feeCurrency, string? note,
        decimal? quantity = null, decimal? taxAmount = null)
    {
        Line(text, "Transaction Id", id);
        Line(text, "Type", type);
        Line(text, "Date", date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        if (quantity is not null)
            Line(text, "Quantity", quantity);
        Line(text, "Amount", amount);
        Line(text, "Currency", currency);
        Line(text, "Fee amount", feeAmount);
        Line(text, "Fee currency", feeCurrency);
        if (taxAmount is not null)
            Line(text, "Tax amount", taxAmount);
        Line(text, "Note", note);
    }

    private static void AppendAccount(StringBuilder text, Account account)
    {
        text.AppendLine("Account");
        Line(text, "Account Id", account.Id);
        Line(text, "Account name", account.Name);
    }

    private static void Line(StringBuilder text, string label, object? value) =>
        text.AppendLine($"{label}: {Convert.ToString(value, CultureInfo.InvariantCulture)}");
}
