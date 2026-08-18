using Microsoft.EntityFrameworkCore;
using PortfolioCalc.Core.Data;
using PortfolioCalc.Core.Data.Prices;
using PortfolioCalc.Core.Data.Repositories;
using PortfolioCalc.Core.Prices;

namespace PortfolioCalc.Core.Tests.Data.Prices;

/// <summary>Integration tests hitting the real Yahoo Finance chart endpoint (per acceptance
/// criterion: "at least one concrete Data/ implementation fetches a real price for a real
/// security"). Uses a past date so the close price is a fixed historical fact and won't
/// drift between test runs.</summary>
public class YahooFinanceSecurityPriceProviderTests
{
    private static readonly DateOnly KnownPastDate = new(2024, 1, 16);

    /// <summary>An open, EnsureCreated in-memory context, which (per PortfolioDbContextTests)
    /// also applies the model's HasData seed — including the "ExchangeYahooSuffix"
    /// vocabulary (LSEETF/LSE -> ".L", IBIS -> ".DE", ARCA/NASDAQ/NYSE -> "") — so these
    /// tests exercise the real, shipped seed data, not a hand-rolled fake mapping.</summary>
    private static PortfolioDbContext CreateSeededContext()
    {
        var options = new DbContextOptionsBuilder<PortfolioDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var context = new PortfolioDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }

    private static YahooFinanceSecurityPriceProvider CreateProvider(PortfolioDbContext context) =>
        new(new HttpClient(), new VocabularyRepository(context));

    [Fact]
    public async Task GetPriceAsync_RealUsTicker_ReturnsSuccessWithKnownPrice()
    {
        using var context = CreateSeededContext();
        var provider = CreateProvider(context);

        var result = await provider.GetPriceAsync("AAPL", "USD", KnownPastDate);

        Assert.Equal(PriceStatus.Success, result.Status);
        Assert.NotNull(result.Price);
        Assert.Equal(183.63m, result.Price!.Value, 2);
    }

    [Fact]
    public async Task GetPriceAsync_UnknownSymbol_ReturnsUnsupportedStatus()
    {
        using var context = CreateSeededContext();
        var provider = CreateProvider(context);

        var result = await provider.GetPriceAsync("ZZZZNOPE", "USD", KnownPastDate);

        Assert.Equal(PriceStatus.UnsupportedSecurity, result.Status);
        Assert.Null(result.Price);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task GetPriceAsync_CurrencyMismatch_ReturnsUnsupportedStatus()
    {
        using var context = CreateSeededContext();
        var provider = CreateProvider(context);

        var result = await provider.GetPriceAsync("AAPL", "EUR", KnownPastDate);

        Assert.Equal(PriceStatus.UnsupportedSecurity, result.Status);
        Assert.Null(result.Price);
    }

    [Fact]
    public async Task GetPriceAsync_LondonListedSecurity_ConvertsPenceToPounds()
    {
        using var context = CreateSeededContext();
        var provider = CreateProvider(context);

        // Yahoo quotes VOD.L in "GBp" (pence): a close of 67.40 GBp is 0.674 GBP.
        var result = await provider.GetPriceAsync("VOD.L", "GBP", KnownPastDate);

        Assert.Equal(PriceStatus.Success, result.Status);
        Assert.NotNull(result.Price);
        Assert.Equal(0.674m, result.Price!.Value, 3);
    }

    [Fact]
    public async Task GetPriceAsync_NetworkFailure_ReturnsNetworkErrorStatus()
    {
        using var context = CreateSeededContext();
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
        var provider = new YahooFinanceSecurityPriceProvider(httpClient, new VocabularyRepository(context), "https://127.0.0.1:1/chart");

        var result = await provider.GetPriceAsync("AAPL", "USD", KnownPastDate);

        Assert.Equal(PriceStatus.NetworkError, result.Status);
        Assert.Null(result.Price);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task GetPriceAsync_ExchangeCode_ResolvesTheYahooSuffixFromTheSeededVocabulary()
    {
        using var context = CreateSeededContext();
        var provider = CreateProvider(context);

        // DB1 is Deutsche Börse (Xetra), traded plain "DB1" won't resolve on Yahoo, but
        // "DB1.DE" does — the "IBIS" -> ".DE" seed row (see PortfolioDbContext.OnModelCreating)
        // makes exchange="IBIS" resolve it without the caller ever mentioning ".DE" itself.
        var withoutExchange = await provider.GetPriceAsync("DB1", "EUR", KnownPastDate);
        var withExchange = await provider.GetPriceAsync("DB1", "EUR", KnownPastDate, exchange: "IBIS");

        Assert.Equal(PriceStatus.UnsupportedSecurity, withoutExchange.Status);
        Assert.Equal(PriceStatus.Success, withExchange.Status);
        Assert.NotNull(withExchange.Price);
    }

    [Fact]
    public async Task GetPriceAsync_UnmappedExchangeCode_FallsBackToThePlainSymbol()
    {
        using var context = CreateSeededContext();
        var provider = CreateProvider(context);

        // "SOMEEXOTICMARKET" has no vocabulary row — falls back to today's behavior
        // (plain symbol), which for AAPL/USD still resolves successfully.
        var result = await provider.GetPriceAsync("AAPL", "USD", KnownPastDate, exchange: "SOMEEXOTICMARKET");

        Assert.Equal(PriceStatus.Success, result.Status);
    }
}
