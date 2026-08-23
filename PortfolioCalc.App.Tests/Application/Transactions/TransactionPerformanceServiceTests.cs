using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PortfolioCalc.App.Application.Fx;
using PortfolioCalc.App.Application.Inflation;
using PortfolioCalc.App.Application.Prices;
using PortfolioCalc.App.Application.Transactions;
using PortfolioCalc.Core.Analytics;
using PortfolioCalc.Core.Data;
using PortfolioCalc.Core.Data.Repositories;
using PortfolioCalc.Core.Domain;
using PortfolioCalc.Core.Fx;
using PortfolioCalc.Core.Inflation;
using PortfolioCalc.Core.Prices;

namespace PortfolioCalc.App.Tests.Application.Transactions;

public class TransactionPerformanceServiceTests
{
    private static readonly DateOnly Today = new(2026, 1, 15);

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

    private sealed class FakePriceProvider(IReadOnlyDictionary<string, decimal> priceBySymbol) : ISecurityPriceProvider
    {
        public Task<PriceResult> GetPriceAsync(
            string symbol, string currency, DateOnly date, string? exchange = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(priceBySymbol.TryGetValue(symbol, out var price)
                ? PriceResult.Ok(price)
                : PriceResult.Unsupported($"No fake price configured for {symbol}"));
    }

    private sealed class FakeFxRateProvider : IFxRateProvider
    {
        public Task<FxRateResult> GetRateAsync(
            string fromCurrency, string toCurrency, DateOnly date, CancellationToken cancellationToken = default) =>
            Task.FromResult(FxRateResult.Ok(1m));
    }

    private sealed class FakeInflationRateProvider(IReadOnlyDictionary<int, decimal>? rateByYear = null) : IInflationRateProvider
    {
        public Task<InflationRateResult> GetRateAsync(
            string baseCurrency, DateOnly period, CancellationToken cancellationToken = default) =>
            Task.FromResult(rateByYear is not null && rateByYear.TryGetValue(period.Year, out var rate)
                ? InflationRateResult.Ok(rate)
                : InflationRateResult.Unsupported($"No fake inflation rate for {period.Year}"));
    }

    private static async Task<(TransactionPerformanceService Service, SecurityTransaction Buy)> CreateServiceAsync(
        PortfolioDbContext context,
        DateOnly buyDate,
        decimal price,
        IReadOnlyDictionary<int, decimal>? inflationRateByYear = null,
        string baseCurrency = "USD")
    {
        var account = await new AccountRepository(context).AddAsync(new Account { Name = "Broker A" });
        var security = await new SecurityRepository(context).AddAsync(
            new Security { Symbol = "AAPL", Name = "AAPL", Currency = "USD" });
        var position = await new PositionRepository(context).AddAsync(
            new Position { AccountId = account.Id, SecurityId = security.Id });

        var settingsRepository = new AppSettingsRepository(context);
        await settingsRepository.SaveAsync(new AppSettings { BaseCurrency = baseCurrency });

        var transactions = new SecurityTransactionRepository(context);
        var buy = await transactions.AddAsync(new SecurityTransaction
        {
            PositionId = position.Id, Type = SecurityTransactionType.Buy, Date = buyDate,
            Quantity = 10m, Amount = -1000m, Currency = "USD",
        });
        buy.Position = position;
        position.Security = security;

        var fxRateService = new FxRateService(new FxRateRepository(context), new FakeFxRateProvider());
        var conversionService = new BaseCurrencyConversionService(settingsRepository, fxRateService);
        var priceService = new SecurityPriceService(
            new SecurityPriceRepository(context),
            new FakePriceProvider(new Dictionary<string, decimal> { ["AAPL"] = price }));
        var inflationRateService = new InflationRateService(
            new InflationRateRepository(context), new FakeInflationRateProvider(inflationRateByYear));

        var service = new TransactionPerformanceService(
            priceService, conversionService, inflationRateService, NullLogger<TransactionPerformanceService>.Instance);

        return (service, buy);
    }

    [Fact]
    public async Task GetBuyPerformanceAsync_computes_cagr_and_cash_flow_result_without_inflation()
    {
        using var context = CreateOpenInMemoryContext();
        // 1461 days = exactly 4 * 365.25 (the calculator's year length), so a clean 10%/year
        // compounds to a clean 1.1^4 = 1.4641 ratio instead of accumulating rounding noise.
        var buyDate = Today.AddDays(-1461);
        var (service, buy) = await CreateServiceAsync(context, buyDate, price: 146.41m);

        var performance = await service.GetBuyPerformanceAsync(buy, inflationAdjusted: false, Today);

        Assert.NotNull(performance.Cagr);
        Assert.Equal(0.1m, performance.Cagr!.Value, 4);
        Assert.Equal(464.1m, performance.CashFlowResult);
    }

    [Fact]
    public async Task GetBuyPerformanceAsync_returns_null_figures_when_the_current_price_cannot_be_resolved()
    {
        using var context = CreateOpenInMemoryContext();
        var account = await new AccountRepository(context).AddAsync(new Account { Name = "Broker A" });
        var security = await new SecurityRepository(context).AddAsync(
            new Security { Symbol = "ZZZZ", Name = "ZZZZ", Currency = "USD" });
        var position = await new PositionRepository(context).AddAsync(
            new Position { AccountId = account.Id, SecurityId = security.Id });
        await new AppSettingsRepository(context).SaveAsync(new AppSettings { BaseCurrency = "USD" });

        var buy = await new SecurityTransactionRepository(context).AddAsync(new SecurityTransaction
        {
            PositionId = position.Id, Type = SecurityTransactionType.Buy, Date = Today.AddDays(-365),
            Quantity = 10m, Amount = -1000m, Currency = "USD",
        });
        buy.Position = position;
        position.Security = security;

        var fxRateService = new FxRateService(new FxRateRepository(context), new FakeFxRateProvider());
        var conversionService = new BaseCurrencyConversionService(new AppSettingsRepository(context), fxRateService);
        var priceService = new SecurityPriceService(
            new SecurityPriceRepository(context), new FakePriceProvider(new Dictionary<string, decimal>()));
        var inflationRateService = new InflationRateService(
            new InflationRateRepository(context), new FakeInflationRateProvider());
        var service = new TransactionPerformanceService(
            priceService, conversionService, inflationRateService, NullLogger<TransactionPerformanceService>.Instance);

        var performance = await service.GetBuyPerformanceAsync(buy, inflationAdjusted: false, Today);

        Assert.Null(performance.Cagr);
        Assert.Null(performance.CashFlowResult);
    }

    [Fact]
    public async Task GetBuyPerformanceAsync_floors_days_elapsed_for_a_very_recent_buy()
    {
        using var context = CreateOpenInMemoryContext();
        var (service, buy) = await CreateServiceAsync(context, Today.AddDays(-2), price: 101m);

        var performance = await service.GetBuyPerformanceAsync(buy, inflationAdjusted: false, Today);

        var expected = GrowthRateCalculator.ComputeAnnualizedRate(1000m, 1010m, GrowthRateCalculator.MinDaysElapsed);
        Assert.Equal(expected, performance.Cagr);
    }

    [Fact]
    public async Task GetBuyPerformanceAsync_applies_inflation_to_the_initial_investment_only()
    {
        using var context = CreateOpenInMemoryContext();
        var buyDate = new DateOnly(2025, 1, 15);
        var (service, buy) = await CreateServiceAsync(
            context, buyDate, price: 100m, inflationRateByYear: new Dictionary<int, decimal> { [2025] = 10m, [2026] = 10m });

        var nominal = await service.GetBuyPerformanceAsync(buy, inflationAdjusted: false, Today);
        var adjusted = await service.GetBuyPerformanceAsync(buy, inflationAdjusted: true, Today);

        Assert.Equal(0m, nominal.CashFlowResult);
        Assert.NotNull(adjusted.CashFlowResult);
        // Inflating the 1000 initial investment forward makes the cash-flow result more negative.
        Assert.True(adjusted.CashFlowResult < nominal.CashFlowResult);
    }
}
