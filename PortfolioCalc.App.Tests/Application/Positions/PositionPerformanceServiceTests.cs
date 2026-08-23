using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PortfolioCalc.App.Application.Fx;
using PortfolioCalc.App.Application.Inflation;
using PortfolioCalc.App.Application.Positions;
using PortfolioCalc.App.Application.Prices;
using PortfolioCalc.Core.Data;
using PortfolioCalc.Core.Data.Repositories;
using PortfolioCalc.Core.Domain;
using PortfolioCalc.Core.Fx;
using PortfolioCalc.Core.Inflation;
using PortfolioCalc.Core.Prices;

namespace PortfolioCalc.App.Tests.Application.Positions;

public class PositionPerformanceServiceTests
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

    private sealed class FakeInflationRateProvider(IReadOnlyDictionary<int, decimal>? rateByYear = null) : IInflationRateProvider
    {
        public Task<InflationRateResult> GetRateAsync(
            string baseCurrency, DateOnly period, CancellationToken cancellationToken = default) =>
            Task.FromResult(rateByYear is not null && rateByYear.TryGetValue(period.Year, out var rate)
                ? InflationRateResult.Ok(rate)
                : InflationRateResult.Unsupported($"No fake inflation rate for {period.Year}"));
    }

    private static async Task<(PositionPerformanceService Service, Position Position)> CreateServiceAsync(
        PortfolioDbContext context,
        IReadOnlyDictionary<string, decimal>? prices = null,
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

        var fxRateService = new FxRateService(new FxRateRepository(context), new FakeFxRateProvider());
        var conversionService = new BaseCurrencyConversionService(settingsRepository, fxRateService);
        var priceService = new SecurityPriceService(
            new SecurityPriceRepository(context), new FakePriceProvider(prices ?? new Dictionary<string, decimal>()));
        var inflationRateService = new InflationRateService(
            new InflationRateRepository(context), new FakeInflationRateProvider(inflationRateByYear));

        var service = new PositionPerformanceService(
            new SecurityTransactionRepository(context), conversionService, priceService, inflationRateService,
            NullLogger<PositionPerformanceService>.Instance);

        return (service, position);
    }

    private sealed class FakeFxRateProvider : IFxRateProvider
    {
        public Task<FxRateResult> GetRateAsync(
            string fromCurrency, string toCurrency, DateOnly date, CancellationToken cancellationToken = default) =>
            Task.FromResult(FxRateResult.Ok(1m));
    }

    [Fact]
    public async Task GetPerformanceAsync_computes_net_invested_dividends_and_fees_without_inflation()
    {
        using var context = CreateOpenInMemoryContext();
        var (service, position) = await CreateServiceAsync(context);
        var transactions = new SecurityTransactionRepository(context);

        await transactions.AddAsync(new SecurityTransaction
        {
            PositionId = position.Id, Type = SecurityTransactionType.Buy, Date = Today.AddDays(-100),
            Quantity = 10m, Amount = -1000m, Currency = "USD", FeeAmount = -5m, FeeCurrency = "USD",
        });
        await transactions.AddAsync(new SecurityTransaction
        {
            PositionId = position.Id, Type = SecurityTransactionType.Sell, Date = Today.AddDays(-50),
            Quantity = 4m, Amount = 480m, Currency = "USD",
        });
        await transactions.AddAsync(new SecurityTransaction
        {
            PositionId = position.Id, Type = SecurityTransactionType.Dividend, Date = Today.AddDays(-30),
            Amount = 20m, Currency = "USD", TaxAmount = -3m,
        });

        var figures = await service.GetPerformanceAsync(position.Id, currentValueInBaseCurrency: 700m, inflationAdjusted: false, Today);

        Assert.True(figures.IsFullyResolved);
        Assert.Equal(1000m - 480m, figures.NetInvested);
        Assert.Equal(20m, figures.TotalDividends);
        Assert.Equal(-5m - 3m, figures.TotalFeesAndTaxes);
        // No inflation: cash-flow result is every transaction's amount + fee/tax, plus the
        // position's current value (the still-held 6 shares).
        Assert.Equal(-1000m - 5m + 480m + 20m - 3m + 700m, figures.CashFlowResult);
    }

    [Fact]
    public async Task GetPerformanceAsync_flags_incomplete_when_the_current_value_cannot_be_resolved()
    {
        using var context = CreateOpenInMemoryContext();
        var (service, position) = await CreateServiceAsync(context);
        var transactions = new SecurityTransactionRepository(context);
        await transactions.AddAsync(new SecurityTransaction
        {
            PositionId = position.Id, Type = SecurityTransactionType.Buy, Date = Today.AddDays(-100),
            Quantity = 10m, Amount = -1000m, Currency = "USD",
        });

        var figures = await service.GetPerformanceAsync(position.Id, currentValueInBaseCurrency: null, inflationAdjusted: false, Today);

        Assert.False(figures.IsFullyResolved);
        Assert.Equal(-1000m, figures.CashFlowResult);
    }

    [Fact]
    public async Task GetPerformanceAsync_values_a_TransferIn_at_the_price_on_its_own_date()
    {
        using var context = CreateOpenInMemoryContext();
        var (service, position) = await CreateServiceAsync(
            context, prices: new Dictionary<string, decimal> { ["AAPL"] = 150m });
        var transactions = new SecurityTransactionRepository(context);

        await transactions.AddAsync(new SecurityTransaction
        {
            PositionId = position.Id, Type = SecurityTransactionType.TransferIn, Date = Today.AddDays(-60),
            Quantity = 5m, Amount = 0m, Currency = "USD",
        });

        var figures = await service.GetPerformanceAsync(position.Id, currentValueInBaseCurrency: 0m, inflationAdjusted: false, Today);

        Assert.True(figures.IsFullyResolved);
        Assert.Equal(750m, figures.NetInvested);
        Assert.Equal(-750m, figures.CashFlowResult);
    }

    [Fact]
    public async Task GetPerformanceAsync_flags_incomplete_when_a_TransferIn_price_cannot_be_resolved()
    {
        using var context = CreateOpenInMemoryContext();
        var (service, position) = await CreateServiceAsync(context);
        var transactions = new SecurityTransactionRepository(context);

        await transactions.AddAsync(new SecurityTransaction
        {
            PositionId = position.Id, Type = SecurityTransactionType.TransferIn, Date = Today.AddDays(-60),
            Quantity = 5m, Amount = 0m, Currency = "USD",
        });

        var figures = await service.GetPerformanceAsync(position.Id, currentValueInBaseCurrency: 0m, inflationAdjusted: false, Today);

        Assert.False(figures.IsFullyResolved);
        Assert.Equal(0m, figures.NetInvested);
        Assert.Equal(0m, figures.CashFlowResult);
    }

    [Fact]
    public async Task GetPerformanceAsync_inflation_adjustment_increases_the_magnitude_of_a_past_cash_flow()
    {
        using var context = CreateOpenInMemoryContext();
        var (service, position) = await CreateServiceAsync(
            context, inflationRateByYear: new Dictionary<int, decimal> { [2025] = 10m, [2026] = 10m });
        var transactions = new SecurityTransactionRepository(context);

        await transactions.AddAsync(new SecurityTransaction
        {
            PositionId = position.Id, Type = SecurityTransactionType.Buy, Date = new DateOnly(2025, 1, 15),
            Quantity = 10m, Amount = -1000m, Currency = "USD",
        });

        var nominal = await service.GetPerformanceAsync(position.Id, currentValueInBaseCurrency: 0m, inflationAdjusted: false, Today);
        var adjusted = await service.GetPerformanceAsync(position.Id, currentValueInBaseCurrency: 0m, inflationAdjusted: true, Today);

        Assert.True(nominal.IsFullyResolved);
        Assert.True(adjusted.IsFullyResolved);
        Assert.Equal(-1000m, nominal.CashFlowResult);
        // A year of 10% inflation makes the same past outflow "worth" more today, in magnitude.
        Assert.True(adjusted.CashFlowResult < nominal.CashFlowResult);
    }
}
