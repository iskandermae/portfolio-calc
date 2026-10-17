using PortfolioCalc.App.Application.Grids;

namespace PortfolioCalc.App.Tests.Application.Grids;

public class AccountCurrencyFilterTests
{
    [Theory]
    [InlineData(null, null, true)]
    [InlineData("acc_a", null, true)]
    [InlineData("acc_b", null, false)]
    [InlineData(null, "USD", true)]
    [InlineData(null, "EUR", false)]
    [InlineData("acc_a", "USD", true)]
    [InlineData("acc_a", "EUR", false)]
    [InlineData("acc_b", "USD", false)]
    public void Matches_combines_account_and_currency_selections(string? account, string? currency, bool expected)
    {
        Assert.Equal(expected, AccountCurrencyFilter.Matches("acc_a", "USD", account, currency));
    }

    [Fact]
    public void Options_are_distinct_and_sorted()
    {
        Assert.Equal(["EUR", "USD"], AccountCurrencyFilter.Options(["USD", "EUR", "USD"]));
    }
}
