namespace PortfolioCalc.App.Application.Grids;

/// <summary>The Account + Currency dropdown filter shared by the Transactions and Portfolio
/// Value grids (story 15). A null selection means "All". Accounts are matched by name.</summary>
public static class AccountCurrencyFilter
{
    public static bool Matches(string account, string currency, string? selectedAccount, string? selectedCurrency) =>
        (selectedAccount is null || account == selectedAccount)
        && (selectedCurrency is null || currency == selectedCurrency);

    /// <summary>Distinct, sorted values actually present in the data, so a selection can
    /// never produce an always-empty grid.</summary>
    public static IReadOnlyList<string> Options(IEnumerable<string> values) =>
        values.Distinct().Order().ToList();
}
