using PortfolioCalc.Core.Data.Prices;
using PortfolioCalc.Core.Prices;

namespace PortfolioCalc.Core.Tests.Data.Prices;

/// <summary>Integration tests hitting the real Yahoo Finance chart endpoint (per acceptance
/// criterion: "at least one concrete Data/ implementation fetches a real price for a real
/// security"). Uses a past date so the close price is a fixed historical fact and won't
/// drift between test runs.</summary>
public class YahooFinanceSecurityPriceProviderTests
{
    private static readonly DateOnly KnownPastDate = new(2024, 1, 16);

    private static YahooFinanceSecurityPriceProvider CreateProvider() => new(new HttpClient());

    [Fact]
    public async Task GetPriceAsync_RealUsTicker_ReturnsSuccessWithKnownPrice()
    {
        var provider = CreateProvider();

        var result = await provider.GetPriceAsync("AAPL", "USD", KnownPastDate);

        Assert.Equal(PriceStatus.Success, result.Status);
        Assert.NotNull(result.Price);
        Assert.Equal(183.63m, result.Price!.Value, 2);
    }

    [Fact]
    public async Task GetPriceAsync_UnknownSymbol_ReturnsUnsupportedStatus()
    {
        var provider = CreateProvider();

        var result = await provider.GetPriceAsync("ZZZZNOPE", "USD", KnownPastDate);

        Assert.Equal(PriceStatus.UnsupportedSecurity, result.Status);
        Assert.Null(result.Price);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task GetPriceAsync_CurrencyMismatch_ReturnsUnsupportedStatus()
    {
        var provider = CreateProvider();

        var result = await provider.GetPriceAsync("AAPL", "EUR", KnownPastDate);

        Assert.Equal(PriceStatus.UnsupportedSecurity, result.Status);
        Assert.Null(result.Price);
    }

    [Fact]
    public async Task GetPriceAsync_LondonListedSecurity_ConvertsPenceToPounds()
    {
        var provider = CreateProvider();

        // Yahoo quotes VOD.L in "GBp" (pence): a close of 67.40 GBp is 0.674 GBP.
        var result = await provider.GetPriceAsync("VOD.L", "GBP", KnownPastDate);

        Assert.Equal(PriceStatus.Success, result.Status);
        Assert.NotNull(result.Price);
        Assert.Equal(0.674m, result.Price!.Value, 3);
    }

    [Fact]
    public async Task GetPriceAsync_NetworkFailure_ReturnsNetworkErrorStatus()
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
        var provider = new YahooFinanceSecurityPriceProvider(httpClient, "https://127.0.0.1:1/chart");

        var result = await provider.GetPriceAsync("AAPL", "USD", KnownPastDate);

        Assert.Equal(PriceStatus.NetworkError, result.Status);
        Assert.Null(result.Price);
        Assert.NotNull(result.ErrorMessage);
    }
}
