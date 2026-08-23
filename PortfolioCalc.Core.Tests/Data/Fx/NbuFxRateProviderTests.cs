using PortfolioCalc.Core.Data.Fx;
using PortfolioCalc.Core.Fx;

namespace PortfolioCalc.Core.Tests.Data.Fx;

/// <summary>Integration tests hitting the real National Bank of Ukraine statistics API —
/// mirrors <see cref="FrankfurterFxRateProviderTests"/>. Uses a past date so the published
/// rate is fixed and won't drift between test runs.</summary>
public class NbuFxRateProviderTests
{
    private static readonly DateOnly KnownPastDate = new(2020, 3, 2);

    private static NbuFxRateProvider CreateProvider() => new(new HttpClient());

    [Fact]
    public async Task GetRateAsync_OtherCurrencyToUah_ReturnsSuccessWithKnownRate()
    {
        var provider = CreateProvider();

        var result = await provider.GetRateAsync("EUR", "UAH", KnownPastDate);

        Assert.Equal(FxRateStatus.Success, result.Status);
        Assert.Equal(26.9789m, result.Rate!.Value, 4);
    }

    [Fact]
    public async Task GetRateAsync_UahToOtherCurrency_InvertsTheRate()
    {
        var provider = CreateProvider();

        var result = await provider.GetRateAsync("UAH", "EUR", KnownPastDate);

        Assert.Equal(FxRateStatus.Success, result.Status);
        Assert.Equal(1m / 26.9789m, result.Rate!.Value, 8);
    }

    [Fact]
    public async Task GetRateAsync_NeitherCurrencyIsUah_ReturnsUnsupportedStatus()
    {
        var provider = CreateProvider();

        var result = await provider.GetRateAsync("USD", "EUR", KnownPastDate);

        Assert.Equal(FxRateStatus.UnsupportedCurrency, result.Status);
    }

    [Fact]
    public async Task GetRateAsync_NetworkFailure_ReturnsNetworkErrorStatus()
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
        var provider = new NbuFxRateProvider(httpClient, "https://127.0.0.1:1/exchange");

        var result = await provider.GetRateAsync("EUR", "UAH", KnownPastDate);

        Assert.Equal(FxRateStatus.NetworkError, result.Status);
    }
}
