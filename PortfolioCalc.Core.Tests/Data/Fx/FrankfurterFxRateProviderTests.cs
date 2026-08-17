using PortfolioCalc.Core.Data.Fx;
using PortfolioCalc.Core.Fx;

namespace PortfolioCalc.Core.Tests.Data.Fx;

/// <summary>Integration tests hitting the real Frankfurter API (per acceptance criterion:
/// "at least one concrete Data/ implementation fetches a real rate for a real currency
/// pair"). Uses a past date so the ECB reference rate is fixed and won't drift between
/// test runs.</summary>
public class FrankfurterFxRateProviderTests
{
    private static readonly DateOnly KnownPastDate = new(2024, 1, 15);

    private static FrankfurterFxRateProvider CreateProvider() => new(new HttpClient());

    [Fact]
    public async Task GetRateAsync_RealCurrencyPair_ReturnsSuccessWithKnownRate()
    {
        var provider = CreateProvider();

        var result = await provider.GetRateAsync("USD", "EUR", KnownPastDate);

        Assert.Equal(FxRateStatus.Success, result.Status);
        Assert.NotNull(result.Rate);
        Assert.Equal(0.91308m, result.Rate!.Value, 3);
    }

    [Fact]
    public async Task GetRateAsync_UnsupportedCurrency_ReturnsUnsupportedStatus()
    {
        var provider = CreateProvider();

        var result = await provider.GetRateAsync("USD", "ZZZ", KnownPastDate);

        Assert.Equal(FxRateStatus.UnsupportedCurrency, result.Status);
        Assert.Null(result.Rate);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task GetRateAsync_NetworkFailure_ReturnsNetworkErrorStatus()
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
        var provider = new FrankfurterFxRateProvider(httpClient, "https://127.0.0.1:1/rates");

        var result = await provider.GetRateAsync("USD", "EUR", KnownPastDate);

        Assert.Equal(FxRateStatus.NetworkError, result.Status);
        Assert.Null(result.Rate);
        Assert.NotNull(result.ErrorMessage);
    }
}
