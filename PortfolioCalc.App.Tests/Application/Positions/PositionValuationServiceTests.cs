using Microsoft.EntityFrameworkCore;
using PortfolioCalc.App.Application.Fx;
using PortfolioCalc.App.Application.Positions;
using PortfolioCalc.App.Application.Prices;
using PortfolioCalc.Core.Data;
using PortfolioCalc.Core.Data.Repositories;
using PortfolioCalc.Core.Domain;
using PortfolioCalc.Core.Fx;
using PortfolioCalc.Core.Prices;

namespace PortfolioCalc.App.Tests.Application.Positions;

public class PositionValuationServiceTests
{
    private static readonly DateOnly AsOf = new(2026, 1, 15);

    private static PortfolioDbContext CreateOpenInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<PortfolioDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var context = new PortfolioDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }

    private sealed class FakePriceProvider(IReadOnlyDictionary<string, PriceResult> resultBySymbol) : ISecurityPriceProvider
    {
        public Task<PriceResult> GetPriceAsync(
            string symbol, string currency, DateOnly date, string? exchange = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(resultBySymbol.TryGetValue(symbol, out var result)
                ? result
                : PriceResult.Unsupported($"No fake price configured for {symbol}"));
    }

    private sealed class FakeFxRateProvider(IReadOnlyDictionary<string, FxRateResult> resultByPair) : IFxRateProvider
    {
        public Task<FxRateResult> GetRateAsync(
            string fromCurrency, string toCurrency, DateOnly date, CancellationToken cancellationToken = default) =>
            Task.FromResult(resultByPair.TryGetValue($"{fromCurrency}/{toCurrency}", out var result)
                ? result
                : FxRateResult.Unsupported($"No fake rate configured for {fromCurrency}/{toCurrency}"));
    }

    private static async Task<(Account account, Security security, Position position)> SeedPositionAsync(
        PortfolioDbContext context, string accountName, string symbol, string currency)
    {
        var account = await new AccountRepository(context).AddAsync(new Account { Name = accountName });
        var security = await new SecurityRepository(context).AddAsync(
            new Security { Symbol = symbol, Name = symbol, Currency = currency });
        var position = await new PositionRepository(context).AddAsync(
            new Position { AccountId = account.Id, SecurityId = security.Id });
        return (account, security, position);
    }

    private static PositionValuationService CreateService(
        PortfolioDbContext context,
        IReadOnlyDictionary<string, PriceResult>? prices = null,
        IReadOnlyDictionary<string, FxRateResult>? fxRates = null,
        string baseCurrency = "USD")
    {
        var transactionRepository = new SecurityTransactionRepository(context);
        var priceService = new SecurityPriceService(
            new SecurityPriceRepository(context), new FakePriceProvider(prices ?? new Dictionary<string, PriceResult>()));
        var fxRateService = new FxRateService(
            new FxRateRepository(context), new FakeFxRateProvider(fxRates ?? new Dictionary<string, FxRateResult>()));
        var settingsRepository = new AppSettingsRepository(context);
        settingsRepository.SaveAsync(new AppSettings { BaseCurrency = baseCurrency }).GetAwaiter().GetResult();
        var conversionService = new BaseCurrencyConversionService(settingsRepository, fxRateService);
        return new PositionValuationService(transactionRepository, priceService, conversionService);
    }

    // --- Position derivation ---

    [Fact]
    public async Task GetCurrentPositionsAsync_excludes_a_fully_sold_position()
    {
        using var context = CreateOpenInMemoryContext();
        var (_, _, position) = await SeedPositionAsync(context, "Broker A", "AAPL", "USD");
        var transactions = new SecurityTransactionRepository(context);
        await transactions.AddAsync(new SecurityTransaction
        {
            PositionId = position.Id, Type = SecurityTransactionType.Buy, Date = AsOf.AddDays(-10),
            Quantity = 10m, Amount = -1000m, Currency = "USD",
        });
        await transactions.AddAsync(new SecurityTransaction
        {
            PositionId = position.Id, Type = SecurityTransactionType.Sell, Date = AsOf.AddDays(-5),
            Quantity = 10m, Amount = 1200m, Currency = "USD",
        });

        var service = CreateService(context);
        var held = await service.GetCurrentPositionsAsync();

        Assert.Empty(held);
    }

    [Fact]
    public async Task GetCurrentPositionsAsync_nets_a_partial_sell_correctly()
    {
        using var context = CreateOpenInMemoryContext();
        var (_, _, position) = await SeedPositionAsync(context, "Broker A", "AAPL", "USD");
        var transactions = new SecurityTransactionRepository(context);
        await transactions.AddAsync(new SecurityTransaction
        {
            PositionId = position.Id, Type = SecurityTransactionType.Buy, Date = AsOf.AddDays(-10),
            Quantity = 10m, Amount = -1000m, Currency = "USD",
        });
        await transactions.AddAsync(new SecurityTransaction
        {
            PositionId = position.Id, Type = SecurityTransactionType.Sell, Date = AsOf.AddDays(-5),
            Quantity = 4m, Amount = 480m, Currency = "USD",
        });

        var service = CreateService(context);
        var held = await service.GetCurrentPositionsAsync();

        var only = Assert.Single(held);
        Assert.Equal(6m, only.Quantity);
    }

    [Fact]
    public async Task GetCurrentPositionsAsync_counts_a_TransferIn_and_ignores_dividend_and_tax_quantity()
    {
        using var context = CreateOpenInMemoryContext();
        var (_, _, position) = await SeedPositionAsync(context, "Broker A", "AAPL", "USD");
        var transactions = new SecurityTransactionRepository(context);
        await transactions.AddAsync(new SecurityTransaction
        {
            PositionId = position.Id, Type = SecurityTransactionType.TransferIn, Date = AsOf.AddDays(-10),
            Quantity = 5m, Amount = 0m, Currency = "USD",
        });
        await transactions.AddAsync(new SecurityTransaction
        {
            PositionId = position.Id, Type = SecurityTransactionType.Dividend, Date = AsOf.AddDays(-5),
            Amount = 10m, Currency = "USD",
        });
        await transactions.AddAsync(new SecurityTransaction
        {
            PositionId = position.Id, Type = SecurityTransactionType.Tax, Date = AsOf.AddDays(-5),
            Amount = -2m, Currency = "USD",
        });

        var service = CreateService(context);
        var held = await service.GetCurrentPositionsAsync();

        var only = Assert.Single(held);
        Assert.Equal(5m, only.Quantity);
    }

    [Fact]
    public async Task GetCurrentPositionsAsync_keeps_the_same_security_in_different_accounts_separate()
    {
        using var context = CreateOpenInMemoryContext();
        var (_, security, _) = await SeedPositionAsync(context, "Broker A", "AAPL", "USD");
        var accountB = await new AccountRepository(context).AddAsync(new Account { Name = "Broker B" });
        var positionB = await new PositionRepository(context).AddAsync(
            new Position { AccountId = accountB.Id, SecurityId = security.Id });

        var transactions = new SecurityTransactionRepository(context);
        var positionAId = (await new PositionRepository(context).GetByAccountAndSecurityAsync(
            (await new AccountRepository(context).GetByNameAsync("Broker A"))!.Id, security.Id))!.Id;
        await transactions.AddAsync(new SecurityTransaction
        {
            PositionId = positionAId, Type = SecurityTransactionType.Buy, Date = AsOf.AddDays(-10),
            Quantity = 10m, Amount = -1000m, Currency = "USD",
        });
        await transactions.AddAsync(new SecurityTransaction
        {
            PositionId = positionB.Id, Type = SecurityTransactionType.Buy, Date = AsOf.AddDays(-10),
            Quantity = 3m, Amount = -300m, Currency = "USD",
        });

        var service = CreateService(context);
        var held = await service.GetCurrentPositionsAsync();

        Assert.Equal(2, held.Count);
        Assert.Contains(held, h => h.Position.AccountId == positionAId && h.Quantity == 10m);
        Assert.Contains(held, h => h.Position.Id == positionB.Id && h.Quantity == 3m);
    }

    // --- Valuation / aggregation ---

    [Fact]
    public async Task GetCurrentValueAsync_computes_per_position_and_grand_total_across_currencies()
    {
        using var context = CreateOpenInMemoryContext();
        var (_, _, positionUsd) = await SeedPositionAsync(context, "Broker A", "AAPL", "USD");
        var (_, _, positionEur) = await SeedPositionAsync(context, "Broker A", "SAP", "EUR");
        var transactions = new SecurityTransactionRepository(context);
        await transactions.AddAsync(new SecurityTransaction
        {
            PositionId = positionUsd.Id, Type = SecurityTransactionType.Buy, Date = AsOf.AddDays(-30),
            Quantity = 10m, Amount = -1000m, Currency = "USD",
        });
        await transactions.AddAsync(new SecurityTransaction
        {
            PositionId = positionEur.Id, Type = SecurityTransactionType.Buy, Date = AsOf.AddDays(-30),
            Quantity = 5m, Amount = -500m, Currency = "EUR",
        });

        var service = CreateService(
            context,
            prices: new Dictionary<string, PriceResult>
            {
                ["AAPL"] = PriceResult.Ok(200m),
                ["SAP"] = PriceResult.Ok(100m),
            },
            fxRates: new Dictionary<string, FxRateResult>
            {
                ["EUR/USD"] = FxRateResult.Ok(1.1m),
            },
            baseCurrency: "USD");

        var report = await service.GetCurrentValueAsync(AsOf);

        Assert.Equal("USD", report.BaseCurrency);
        Assert.Equal(2, report.Positions.Count);

        var aapl = report.Positions.Single(p => p.SecuritySymbol == "AAPL");
        Assert.True(aapl.IsResolved);
        Assert.Equal(2000m, aapl.ValueInSecurityCurrency);
        Assert.Equal(2000m, aapl.ValueInBaseCurrency);

        var sap = report.Positions.Single(p => p.SecuritySymbol == "SAP");
        Assert.True(sap.IsResolved);
        Assert.Equal(500m, sap.ValueInSecurityCurrency);
        Assert.Equal(550m, sap.ValueInBaseCurrency);

        Assert.Equal(2550m, report.GrandTotalInBaseCurrency);
    }

    [Fact]
    public async Task GetCurrentValueAsync_flags_and_excludes_a_position_whose_price_cannot_be_resolved()
    {
        using var context = CreateOpenInMemoryContext();
        var (_, _, positionUsd) = await SeedPositionAsync(context, "Broker A", "AAPL", "USD");
        var (_, _, positionUnpriced) = await SeedPositionAsync(context, "Broker A", "ZZZZ", "USD");
        var transactions = new SecurityTransactionRepository(context);
        await transactions.AddAsync(new SecurityTransaction
        {
            PositionId = positionUsd.Id, Type = SecurityTransactionType.Buy, Date = AsOf.AddDays(-30),
            Quantity = 10m, Amount = -1000m, Currency = "USD",
        });
        await transactions.AddAsync(new SecurityTransaction
        {
            PositionId = positionUnpriced.Id, Type = SecurityTransactionType.Buy, Date = AsOf.AddDays(-30),
            Quantity = 7m, Amount = -700m, Currency = "USD",
        });

        var service = CreateService(
            context,
            prices: new Dictionary<string, PriceResult> { ["AAPL"] = PriceResult.Ok(200m) },
            baseCurrency: "USD");

        var report = await service.GetCurrentValueAsync(AsOf);

        var unpriced = report.Positions.Single(p => p.SecuritySymbol == "ZZZZ");
        Assert.False(unpriced.IsResolved);
        Assert.Null(unpriced.Price);
        Assert.Null(unpriced.ValueInBaseCurrency);

        var priced = report.Positions.Single(p => p.SecuritySymbol == "AAPL");
        Assert.True(priced.IsResolved);

        // Grand total only sums the resolved position.
        Assert.Equal(2000m, report.GrandTotalInBaseCurrency);
    }

    [Fact]
    public async Task GetCurrentValueAsync_flags_and_excludes_a_position_whose_fx_rate_cannot_be_resolved()
    {
        using var context = CreateOpenInMemoryContext();
        var (_, _, positionEur) = await SeedPositionAsync(context, "Broker A", "SAP", "EUR");
        var transactions = new SecurityTransactionRepository(context);
        await transactions.AddAsync(new SecurityTransaction
        {
            PositionId = positionEur.Id, Type = SecurityTransactionType.Buy, Date = AsOf.AddDays(-30),
            Quantity = 5m, Amount = -500m, Currency = "EUR",
        });

        // No EUR/USD fake rate configured, so the FX lookup fails.
        var service = CreateService(
            context,
            prices: new Dictionary<string, PriceResult> { ["SAP"] = PriceResult.Ok(100m) },
            baseCurrency: "USD");

        var report = await service.GetCurrentValueAsync(AsOf);

        var sap = Assert.Single(report.Positions);
        Assert.False(sap.IsResolved);
        Assert.Equal(500m, sap.ValueInSecurityCurrency);
        Assert.Null(sap.ValueInBaseCurrency);
        Assert.Equal(0m, report.GrandTotalInBaseCurrency);
    }
}
