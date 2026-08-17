using PortfolioCalc.Core.Data.Inflation;
using PortfolioCalc.Core.Inflation;

namespace PortfolioCalc.Core.Tests.Data.Inflation;

/// <summary>Integration tests hitting the real World Bank API, mirroring
/// FrankfurterFxRateProviderTests's style. Uses a past year so the published inflation
/// figure is final and won't drift between test runs.</summary>
public class WorldBankInflationRateProviderTests
{
    private static readonly DateOnly KnownPastPeriod = new(2021, 1, 1);

    private static WorldBankInflationRateProvider CreateProvider() => new(new HttpClient());

    [Fact]
    public async Task GetRateAsync_RealCurrency_ReturnsSuccessWithKnownRate()
    {
        var provider = CreateProvider();

        var result = await provider.GetRateAsync("USD", KnownPastPeriod);

        Assert.Equal(InflationRateStatus.Success, result.Status);
        Assert.NotNull(result.Rate);
        Assert.Equal(4.7m, result.Rate!.Value, 1);
    }

    [Fact]
    public async Task GetRateAsync_UnmappedCurrency_ReturnsUnsupportedStatus()
    {
        var provider = CreateProvider();

        var result = await provider.GetRateAsync("ZZZ", KnownPastPeriod);

        Assert.Equal(InflationRateStatus.UnsupportedCurrency, result.Status);
        Assert.Null(result.Rate);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task GetRateAsync_NetworkFailure_ReturnsNetworkErrorStatus()
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
        var provider = new WorldBankInflationRateProvider(httpClient, "https://127.0.0.1:1/country");

        var result = await provider.GetRateAsync("USD", KnownPastPeriod);

        Assert.Equal(InflationRateStatus.NetworkError, result.Status);
        Assert.Null(result.Rate);
        Assert.NotNull(result.ErrorMessage);
    }
}
