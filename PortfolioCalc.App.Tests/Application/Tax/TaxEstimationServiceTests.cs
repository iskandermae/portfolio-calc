using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PortfolioCalc.App.Application.Fx;
using PortfolioCalc.App.Application.Prices;
using PortfolioCalc.App.Application.Tax;
using PortfolioCalc.Core.Data;
using PortfolioCalc.Core.Data.Repositories;
using PortfolioCalc.Core.Domain;
using PortfolioCalc.Core.Fx;
using PortfolioCalc.Core.Prices;

namespace PortfolioCalc.App.Tests.Application.Tax;

public class TaxEstimationServiceTests
{
    private static readonly DateOnly Today = new(2026, 6, 1);
    private static readonly DateOnly TaxYearStart = new(2026, 4, 6);

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

    private sealed class FakePriceProvider(
        IReadOnlyDictionary<(string Symbol, DateOnly Date), decimal> priceBySymbolAndDate) : ISecurityPriceProvider
    {
        public Task<PriceResult> GetPriceAsync(
            string symbol, string currency, DateOnly date, string? exchange = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(priceBySymbolAndDate.TryGetValue((symbol, date), out var price)
                ? PriceResult.Ok(price)
                : PriceResult.Unsupported($"No fake price configured for {symbol} on {date}"));
    }

    private sealed class FakeFxRateProvider(IReadOnlyDictionary<(string Pair, DateOnly Date), decimal>? rateByPairAndDate = null) : IFxRateProvider
    {
        public Task<FxRateResult> GetRateAsync(
            string fromCurrency, string toCurrency, DateOnly date, CancellationToken cancellationToken = default)
        {
            if (string.Equals(fromCurrency, toCurrency, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(FxRateResult.Ok(1m));

            var key = ($"{fromCurrency}/{toCurrency}", date);
            return Task.FromResult(rateByPairAndDate is not null && rateByPairAndDate.TryGetValue(key, out var rate)
                ? FxRateResult.Ok(rate)
                : FxRateResult.Unsupported($"No fake rate configured for {fromCurrency}/{toCurrency} on {date}"));
        }
    }

    private static async Task<(TaxEstimationService Service, Position Position, Security Security)> CreateServiceAsync(
        PortfolioDbContext context,
        IReadOnlyDictionary<(string Symbol, DateOnly Date), decimal>? prices = null,
        IReadOnlyDictionary<(string Pair, DateOnly Date), decimal>? fxRates = null,
        string taxBaseCurrency = "USD")
    {
        var account = await new AccountRepository(context).AddAsync(new Account { Name = "Broker A" });
        var security = await new SecurityRepository(context).AddAsync(
            new Security { Symbol = "AAPL", Name = "AAPL", Currency = "USD" });
        var position = await new PositionRepository(context).AddAsync(
            new Position { AccountId = account.Id, SecurityId = security.Id });

        var settingsRepository = new AppSettingsRepository(context);
        await settingsRepository.SaveAsync(new AppSettings { BaseCurrency = "USD", TaxBaseCurrency = taxBaseCurrency });

        var fxRateService = new FxRateService(
            new FxRateRepository(context), new FakeFxRateProvider(fxRates));
        var priceService = new SecurityPriceService(
            new SecurityPriceRepository(context),
            new FakePriceProvider(prices ?? new Dictionary<(string, DateOnly), decimal>()));

        var service = new TaxEstimationService(
            new SecurityTransactionRepository(context), settingsRepository,
            priceService, fxRateService, NullLogger<TaxEstimationService>.Instance);

        return (service, position, security);
    }

    [Fact]
    public async Task ComputeAsync_computes_gain_for_an_actual_sell_using_average_cost()
    {
        using var context = CreateOpenInMemoryContext();
        var (service, position, _) = await CreateServiceAsync(context);
        var transactions = new SecurityTransactionRepository(context);

        await transactions.AddAsync(new SecurityTransaction
        {
            PositionId = position.Id, Type = SecurityTransactionType.Buy, Date = new DateOnly(2020, 1, 1),
            Quantity = 10m, Amount = -1000m, Currency = "USD",
        });
        await transactions.AddAsync(new SecurityTransaction
        {
            PositionId = position.Id, Type = SecurityTransactionType.Sell, Date = new DateOnly(2026, 5, 1),
            Quantity = 5m, Amount = 750m, Currency = "USD",
        });

        var report = await service.ComputeAsync(TaxYearStart, [], Today);

        var row = Assert.Single(report.Rows);
        Assert.Equal(5m, row.QuantitySold);
        Assert.Equal(500m, row.AverageBuyCostInSecurityCurrency); // 5 shares * $100 avg cost
        Assert.Equal(750m, row.SellAmountInSecurityCurrency);
        Assert.Equal(250m, row.GainInBaseCurrency); // same currency, rate 1
        Assert.Equal(250m, report.TotalGainInBaseCurrency);
    }

    [Fact]
    public async Task ComputeAsync_blends_average_cost_across_buys_at_different_prices()
    {
        using var context = CreateOpenInMemoryContext();
        var (service, position, _) = await CreateServiceAsync(context);
        var transactions = new SecurityTransactionRepository(context);

        await transactions.AddAsync(new SecurityTransaction
        {
            PositionId = position.Id, Type = SecurityTransactionType.Buy, Date = new DateOnly(2020, 1, 1),
            Quantity = 5m, Amount = -500m, Currency = "USD", // $100/share
        });
        await transactions.AddAsync(new SecurityTransaction
        {
            PositionId = position.Id, Type = SecurityTransactionType.Buy, Date = new DateOnly(2021, 1, 1),
            Quantity = 5m, Amount = -600m, Currency = "USD", // $120/share
        });
        await transactions.AddAsync(new SecurityTransaction
        {
            PositionId = position.Id, Type = SecurityTransactionType.Sell, Date = new DateOnly(2026, 5, 1),
            Quantity = 10m, Amount = 1300m, Currency = "USD",
        });

        var report = await service.ComputeAsync(TaxYearStart, [], Today);

        var row = Assert.Single(report.Rows);
        // Average cost = (500 + 600) / 10 = 110/share; 10 shares sold => 1100 cost basis.
        Assert.Equal(1100m, row.AverageBuyCostInSecurityCurrency);
        Assert.Equal(200m, row.GainInBaseCurrency);
    }

    [Fact]
    public async Task ComputeAsync_values_a_proposed_sell_using_the_last_available_price_within_the_lookback()
    {
        using var context = CreateOpenInMemoryContext();
        var (service, position, _) = await CreateServiceAsync(
            context, prices: new Dictionary<(string, DateOnly), decimal> { [("AAPL", Today.AddDays(-3))] = 150m });
        var transactions = new SecurityTransactionRepository(context);

        await transactions.AddAsync(new SecurityTransaction
        {
            PositionId = position.Id, Type = SecurityTransactionType.Buy, Date = new DateOnly(2020, 1, 1),
            Quantity = 10m, Amount = -1000m, Currency = "USD",
        });

        var report = await service.ComputeAsync(TaxYearStart, [new ProposedSell(position.Id, 4m)], Today);

        var row = Assert.Single(report.Rows);
        Assert.Equal(4m, row.QuantitySold);
        Assert.Equal(600m, row.SellAmountInSecurityCurrency); // 4 * 150 (the 3-days-back fallback price)
    }

    [Fact]
    public async Task ComputeAsync_throws_when_a_proposed_sell_exceeds_the_currently_held_quantity()
    {
        using var context = CreateOpenInMemoryContext();
        var (service, position, _) = await CreateServiceAsync(context);
        var transactions = new SecurityTransactionRepository(context);
        await transactions.AddAsync(new SecurityTransaction
        {
            PositionId = position.Id, Type = SecurityTransactionType.Buy, Date = new DateOnly(2020, 1, 1),
            Quantity = 10m, Amount = -1000m, Currency = "USD",
        });

        await Assert.ThrowsAsync<TaxEstimationException>(
            () => service.ComputeAsync(TaxYearStart, [new ProposedSell(position.Id, 11m)], Today));
    }

    [Fact]
    public async Task ComputeAsync_throws_when_a_historical_buy_fx_rate_cannot_be_resolved()
    {
        using var context = CreateOpenInMemoryContext();
        var (service, position, _) = await CreateServiceAsync(context, taxBaseCurrency: "EUR");
        // No EUR fake rate configured, so converting the USD buy cost fails.
        var transactions = new SecurityTransactionRepository(context);
        await transactions.AddAsync(new SecurityTransaction
        {
            PositionId = position.Id, Type = SecurityTransactionType.Buy, Date = new DateOnly(2020, 1, 1),
            Quantity = 10m, Amount = -1000m, Currency = "USD",
        });
        await transactions.AddAsync(new SecurityTransaction
        {
            PositionId = position.Id, Type = SecurityTransactionType.Sell, Date = new DateOnly(2026, 5, 1),
            Quantity = 5m, Amount = 750m, Currency = "USD",
        });

        await Assert.ThrowsAsync<TaxEstimationException>(() => service.ComputeAsync(TaxYearStart, [], Today));
    }

    [Fact]
    public async Task ComputeAsync_includes_a_TransferIn_valued_at_its_own_date_price_in_the_average_cost()
    {
        using var context = CreateOpenInMemoryContext();
        var (service, position, _) = await CreateServiceAsync(
            context, prices: new Dictionary<(string, DateOnly), decimal> { [("AAPL", new DateOnly(2020, 6, 1))] = 80m });
        var transactions = new SecurityTransactionRepository(context);

        await transactions.AddAsync(new SecurityTransaction
        {
            PositionId = position.Id, Type = SecurityTransactionType.TransferIn, Date = new DateOnly(2020, 6, 1),
            Quantity = 10m, Amount = 0m, Currency = "USD",
        });
        await transactions.AddAsync(new SecurityTransaction
        {
            PositionId = position.Id, Type = SecurityTransactionType.Sell, Date = new DateOnly(2026, 5, 1),
            Quantity = 10m, Amount = 900m, Currency = "USD",
        });

        var report = await service.ComputeAsync(TaxYearStart, [], Today);

        var row = Assert.Single(report.Rows);
        Assert.Equal(800m, row.AverageBuyCostInSecurityCurrency); // 10 shares * $80
        Assert.Equal(100m, row.GainInBaseCurrency);
    }

    [Fact]
    public async Task ComputeAsync_never_mixes_the_same_security_held_in_two_different_accounts()
    {
        using var context = CreateOpenInMemoryContext();
        var (service, positionA, security) = await CreateServiceAsync(context);
        var accountB = await new AccountRepository(context).AddAsync(new Account { Name = "Broker B" });
        var positionB = await new PositionRepository(context).AddAsync(
            new Position { AccountId = accountB.Id, SecurityId = security.Id });
        var transactions = new SecurityTransactionRepository(context);

        // Account A: bought at $100/share, sold at $150/share -> $50/share gain.
        await transactions.AddAsync(new SecurityTransaction
        {
            PositionId = positionA.Id, Type = SecurityTransactionType.Buy, Date = new DateOnly(2020, 1, 1),
            Quantity = 10m, Amount = -1000m, Currency = "USD",
        });
        await transactions.AddAsync(new SecurityTransaction
        {
            PositionId = positionA.Id, Type = SecurityTransactionType.Sell, Date = new DateOnly(2026, 5, 1),
            Quantity = 10m, Amount = 1500m, Currency = "USD",
        });

        // Account B: bought at $300/share (a much higher cost basis), sold at $310/share.
        await transactions.AddAsync(new SecurityTransaction
        {
            PositionId = positionB.Id, Type = SecurityTransactionType.Buy, Date = new DateOnly(2021, 1, 1),
            Quantity = 5m, Amount = -1500m, Currency = "USD",
        });
        await transactions.AddAsync(new SecurityTransaction
        {
            PositionId = positionB.Id, Type = SecurityTransactionType.Sell, Date = new DateOnly(2026, 5, 1),
            Quantity = 5m, Amount = 1550m, Currency = "USD",
        });

        var report = await service.ComputeAsync(TaxYearStart, [], Today);

        Assert.Equal(2, report.Rows.Count);
        var rowA = report.Rows.Single(r => r.AccountName == "Broker A");
        var rowB = report.Rows.Single(r => r.AccountName == "Broker B");
        // If cost bases were blended across accounts, both rows would show the same
        // (wrong) average cost instead of each account's own $100/$300 per share.
        Assert.Equal(1000m, rowA.AverageBuyCostInSecurityCurrency);
        Assert.Equal(500m, rowA.GainInBaseCurrency);
        Assert.Equal(1500m, rowB.AverageBuyCostInSecurityCurrency);
        Assert.Equal(50m, rowB.GainInBaseCurrency);
    }

    [Fact]
    public async Task ComputeAsync_an_account_filter_excludes_other_accounts_entirely()
    {
        using var context = CreateOpenInMemoryContext();
        var (service, positionA, security) = await CreateServiceAsync(context);
        var accountB = await new AccountRepository(context).AddAsync(new Account { Name = "Broker B" });
        var positionB = await new PositionRepository(context).AddAsync(
            new Position { AccountId = accountB.Id, SecurityId = security.Id });
        var transactions = new SecurityTransactionRepository(context);

        await transactions.AddAsync(new SecurityTransaction
        {
            PositionId = positionA.Id, Type = SecurityTransactionType.Buy, Date = new DateOnly(2020, 1, 1),
            Quantity = 10m, Amount = -1000m, Currency = "USD",
        });
        await transactions.AddAsync(new SecurityTransaction
        {
            PositionId = positionA.Id, Type = SecurityTransactionType.Sell, Date = new DateOnly(2026, 5, 1),
            Quantity = 10m, Amount = 1500m, Currency = "USD",
        });
        await transactions.AddAsync(new SecurityTransaction
        {
            PositionId = positionB.Id, Type = SecurityTransactionType.Buy, Date = new DateOnly(2021, 1, 1),
            Quantity = 5m, Amount = -1500m, Currency = "USD",
        });
        await transactions.AddAsync(new SecurityTransaction
        {
            PositionId = positionB.Id, Type = SecurityTransactionType.Sell, Date = new DateOnly(2026, 5, 1),
            Quantity = 5m, Amount = 1550m, Currency = "USD",
        });

        var report = await service.ComputeAsync(TaxYearStart, [], Today, accountIdFilter: positionA.AccountId);

        var row = Assert.Single(report.Rows);
        Assert.Equal("Broker A", row.AccountName);
    }

    [Fact]
    public async Task ComputeAsync_held_quantity_validation_is_scoped_to_one_position_not_the_whole_security()
    {
        using var context = CreateOpenInMemoryContext();
        var (service, positionA, security) = await CreateServiceAsync(context);
        var accountB = await new AccountRepository(context).AddAsync(new Account { Name = "Broker B" });
        var positionB = await new PositionRepository(context).AddAsync(
            new Position { AccountId = accountB.Id, SecurityId = security.Id });
        var transactions = new SecurityTransactionRepository(context);

        // Account A holds 10, account B holds 10 (20 total for the security) — but a
        // proposed sell of 10 from account B alone must not be allowed to "borrow"
        // account A's holding.
        await transactions.AddAsync(new SecurityTransaction
        {
            PositionId = positionA.Id, Type = SecurityTransactionType.Buy, Date = new DateOnly(2020, 1, 1),
            Quantity = 10m, Amount = -1000m, Currency = "USD",
        });

        await Assert.ThrowsAsync<TaxEstimationException>(
            () => service.ComputeAsync(TaxYearStart, [new ProposedSell(positionB.Id, 5m)], Today));
    }
}
