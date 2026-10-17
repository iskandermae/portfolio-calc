using PortfolioCalc.App.Application.Positions;

namespace PortfolioCalc.App.Tests.Application.Positions;

public class PortfolioValueGridViewTests
{
    private static PositionValuation Valuation(
        int id, string account, string symbol, string currency, decimal? baseValue, bool resolved = true) =>
        new(id, account, symbol, currency, 1m, 1m, baseValue, baseValue, resolved);

    private static readonly PortfolioValueReport Report = new(
    [
        Valuation(1, "acc_a", "AAA", "USD", 100m),
        Valuation(2, "acc_a", "BBB", "EUR", 50m),
        Valuation(3, "acc_b", "CCC", "USD", 25m),
        Valuation(4, "acc_b", "DDD", "USD", null, resolved: false),
    ], 175m, "EUR");

    [Fact]
    public void No_filter_keeps_all_positions_and_total_excludes_unresolved()
    {
        var view = PortfolioValueGridView.Apply(Report, null, null, "Account", false);

        Assert.Equal(4, view.Positions.Count);
        Assert.Equal(175m, view.GrandTotalInBaseCurrency);
        Assert.Equal("EUR", view.BaseCurrency);
    }

    [Fact]
    public void Account_filter_recomputes_total_over_visible_positions()
    {
        var view = PortfolioValueGridView.Apply(Report, "acc_a", null, "Account", false);

        Assert.Equal([1, 2], view.Positions.Select(p => p.PositionId));
        Assert.Equal(150m, view.GrandTotalInBaseCurrency);
    }

    [Fact]
    public void Currency_filter_matches_security_currency_and_still_excludes_unresolved()
    {
        var view = PortfolioValueGridView.Apply(Report, null, "USD", "Account", false);

        Assert.Equal([1, 3, 4], view.Positions.Select(p => p.PositionId));
        Assert.Equal(125m, view.GrandTotalInBaseCurrency);
    }

    [Fact]
    public void Account_and_currency_filters_combine_and_can_yield_empty_with_zero_total()
    {
        Assert.Equal([3], PortfolioValueGridView.Apply(Report, "acc_b", "USD", "Account", false)
            .Positions.Where(p => p.IsResolved).Select(p => p.PositionId));

        var empty = PortfolioValueGridView.Apply(Report, "acc_a", "GBP", "Account", false);
        Assert.Empty(empty.Positions);
        Assert.Equal(0m, empty.GrandTotalInBaseCurrency);
    }

    [Fact]
    public void Sorts_by_key_ascending_and_descending_after_filtering()
    {
        var ascending = PortfolioValueGridView.Apply(Report, null, "USD", "ValueInBaseCurrency", false);
        var descending = PortfolioValueGridView.Apply(Report, null, "USD", "ValueInBaseCurrency", true);

        Assert.Equal([4, 3, 1], ascending.Positions.Select(p => p.PositionId));
        Assert.Equal([1, 3, 4], descending.Positions.Select(p => p.PositionId));
    }
}
