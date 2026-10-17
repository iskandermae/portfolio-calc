using PortfolioCalc.App.Application.Grids;

namespace PortfolioCalc.App.Application.Positions;

/// <summary>Filters and sorts a loaded <see cref="PortfolioValueReport"/> for the grid
/// (story 15) without re-fetching anything. The grand total is recomputed over the filtered
/// positions only, still excluding unresolved ones (as in story 09). The currency filter
/// matches the position's security currency.</summary>
public static class PortfolioValueGridView
{
    public static PortfolioValueReport Apply(
        PortfolioValueReport report, string? account, string? currency, string sortKey, bool descending)
    {
        var filtered = report.Positions
            .Where(p => AccountCurrencyFilter.Matches(p.AccountName, p.SecurityCurrency, account, currency));

        IEnumerable<PositionValuation> ordered = sortKey switch
        {
            "Security" => filtered.OrderBy(p => p.SecuritySymbol),
            "Quantity" => filtered.OrderBy(p => p.Quantity),
            "Price" => filtered.OrderBy(p => p.Price),
            "ValueInSecurityCurrency" => filtered.OrderBy(p => p.ValueInSecurityCurrency),
            "ValueInBaseCurrency" => filtered.OrderBy(p => p.ValueInBaseCurrency),
            _ => filtered.OrderBy(p => p.AccountName),
        };
        var positions = (descending ? ordered.Reverse() : ordered).ToList();

        var total = positions.Where(p => p.IsResolved).Sum(p => p.ValueInBaseCurrency!.Value);
        return report with { Positions = positions, GrandTotalInBaseCurrency = total };
    }
}
