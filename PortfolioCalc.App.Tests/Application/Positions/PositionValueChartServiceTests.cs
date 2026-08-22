using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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

public class PositionValueChartServiceTests
{
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
        IReadOnlyDictionary<string, decimal> constantPriceBySymbol,
        IReadOnlySet<(string Symbol, DateOnly Date)>? gaps = null) : ISecurityPriceProvider
    {
        public Task<PriceResult> GetPriceAsync(
            string symbol, string currency, DateOnly date, string? exchange = null,
            CancellationToken cancellationToken = default)
        {
            if (gaps?.Contains((symbol, date)) == true)
                return Task.FromResult(PriceResult.Unsupported($"No fake price for {symbol} on {date}"));

            return Task.FromResult(constantPriceBySymbol.TryGetValue(symbol, out var price)
                ? PriceResult.Ok(price)
                : PriceResult.Unsupported($"No fake price configured for {symbol}"));
        }
    }

    private sealed class FakeFxRateProvider(decimal rate = 1m) : IFxRateProvider
    {
        public Task<FxRateResult> GetRateAsync(
            string fromCurrency, string toCurrency, DateOnly date, CancellationToken cancellationToken = default) =>
            Task.FromResult(FxRateResult.Ok(rate));
    }

    private sealed class FakeInflationRateProvider(IReadOnlyDictionary<int, decimal>? rateByYear = null) : IInflationRateProvider
    {
        public bool WasCalled { get; private set; }

        public Task<InflationRateResult> GetRateAsync(
            string baseCurrency, DateOnly period, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(rateByYear is not null && rateByYear.TryGetValue(period.Year, out var rate)
                ? InflationRateResult.Ok(rate)
                : InflationRateResult.Unsupported($"No fake inflation rate for {period.Year}"));
        }
    }

    /// <summary>Captures every <c>LogWarning</c> message so tests can assert a missing
    /// inflation rate is actually surfaced to the Logs page, without depending on a real
    /// file-backed logger.</summary>
    private sealed class CapturingLogger : ILogger<PositionValueChartService>
    {
        public List<string> WarningMessages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
                WarningMessages.Add(formatter(state, exception));
        }
    }

    private static (PositionValueChartService Service, Security Security) CreateService(
        PortfolioDbContext context,
        IReadOnlyDictionary<string, decimal> prices,
        IReadOnlySet<(string Symbol, DateOnly Date)>? gaps = null,
        decimal fxRate = 1m,
        IReadOnlyDictionary<int, decimal>? inflationRates = null,
        FakeInflationRateProvider? inflationProviderOverride = null,
        ILogger<PositionValueChartService>? logger = null)
    {
        var securityRepository = new SecurityRepository(context);
        var priceService = new SecurityPriceService(
            new SecurityPriceRepository(context), new FakePriceProvider(prices, gaps));
        var fxRateService = new FxRateService(new FxRateRepository(context), new FakeFxRateProvider(fxRate));
        var inflationProvider = inflationProviderOverride ?? new FakeInflationRateProvider(inflationRates);
        var inflationRateService = new InflationRateService(new InflationRateRepository(context), inflationProvider);

        var security = securityRepository.AddAsync(
            new Security { Symbol = "AAPL", Name = "AAPL", Currency = "USD" }).GetAwaiter().GetResult();

        var service = new PositionValueChartService(
            securityRepository, priceService, fxRateService, inflationRateService,
            logger ?? NullLogger<PositionValueChartService>.Instance);
        return (service, security);
    }

    [Fact]
    public async Task BuildChartAsync_produces_matching_start_values_for_primary_and_comparison_series()
    {
        using var context = CreateOpenInMemoryContext();
        var (service, security) = CreateService(
            context,
            prices: new Dictionary<string, decimal> { ["AAPL"] = 100m, ["CSPX.L"] = 50m });

        var start = new DateOnly(2026, 1, 1);
        var today = new DateOnly(2026, 1, 7);

        var result = await service.BuildChartAsync(security, start, "USD", 10m, inflationAdjusted: false, today);

        Assert.Equal(2, result.PrimarySeries.Count);
        Assert.Equal(2, result.ComparisonSeries.Count);

        // shares(10) * price(100) = 1000 at both sample dates (constant price).
        Assert.All(result.PrimarySeries, p => Assert.Equal(1000m, p.ValueInBaseCurrency));

        // Both series must start at the same value on the start date.
        var primaryStart = result.PrimarySeries.Single(p => p.Date == start).ValueInBaseCurrency;
        var comparisonStart = result.ComparisonSeries.Single(p => p.Date == start).ValueInBaseCurrency;
        Assert.Equal(primaryStart, comparisonStart);

        // Comparison series stays constant too, since CSPX.L's price is also constant.
        Assert.All(result.ComparisonSeries, p => Assert.Equal(1000m, p.ValueInBaseCurrency));
    }

    [Fact]
    public async Task BuildChartAsync_excludes_a_sample_point_with_no_resolvable_price_instead_of_throwing()
    {
        using var context = CreateOpenInMemoryContext();
        var start = new DateOnly(2026, 1, 1);
        var today = new DateOnly(2026, 1, 7);

        // No price at all (within the 7-day lookback) for the "today" sample date.
        var gaps = Enumerable.Range(0, 7)
            .Select(offset => ("AAPL", today.AddDays(-offset)))
            .ToHashSet();

        var (service, security) = CreateService(
            context,
            prices: new Dictionary<string, decimal> { ["AAPL"] = 100m, ["CSPX.L"] = 50m },
            gaps: gaps);

        var result = await service.BuildChartAsync(security, start, "USD", 10m, inflationAdjusted: false, today);

        Assert.Single(result.PrimarySeries);
        Assert.Equal(start, result.PrimarySeries[0].Date);
    }

    [Fact]
    public async Task BuildChartAsync_never_calls_the_inflation_provider_when_the_toggle_is_off()
    {
        using var context = CreateOpenInMemoryContext();
        var inflationProvider = new FakeInflationRateProvider();
        var (service, security) = CreateService(
            context,
            prices: new Dictionary<string, decimal> { ["AAPL"] = 100m, ["CSPX.L"] = 50m },
            inflationProviderOverride: inflationProvider);

        await service.BuildChartAsync(
            security, new DateOnly(2026, 1, 1), "USD", 10m, inflationAdjusted: false, new DateOnly(2026, 1, 7));

        Assert.False(inflationProvider.WasCalled);
    }

    [Fact]
    public async Task BuildChartAsync_applies_the_forward_inflation_adjustment_when_the_toggle_is_on()
    {
        using var context = CreateOpenInMemoryContext();
        var start = new DateOnly(2026, 1, 1);
        var today = new DateOnly(2026, 7, 1); // 181 days into a 365-day year
        var (service, security) = CreateService(
            context,
            prices: new Dictionary<string, decimal> { ["AAPL"] = 100m, ["CSPX.L"] = 50m },
            // Matches InflationRate.Rate's percentage convention (5m for 5%), not a
            // fraction — the service divides by 100 before feeding the pure calculator.
            inflationRates: new Dictionary<int, decimal> { [2026] = 5m });

        var result = await service.BuildChartAsync(security, start, "USD", 10m, inflationAdjusted: true, today);

        var startPoint = result.PrimarySeries.Single(p => p.Date == start);
        var expectedFactor = (decimal)Math.Pow(1.05, 181.0 / 365.0);
        Assert.Equal(1000m * expectedFactor, startPoint.ValueInBaseCurrency, precision: 6);

        // The "today" point needs no adjustment (0 active days in the span left to accrue).
        var todayPoint = result.PrimarySeries.Single(p => p.Date == today);
        Assert.Equal(1000m, todayPoint.ValueInBaseCurrency, precision: 6);
    }

    [Fact]
    public async Task BuildChartAsync_logs_a_warning_when_a_years_inflation_rate_cannot_be_resolved()
    {
        using var context = CreateOpenInMemoryContext();
        var logger = new CapturingLogger();
        // No inflation rates configured at all -> every year lookup fails.
        var (service, security) = CreateService(
            context,
            prices: new Dictionary<string, decimal> { ["AAPL"] = 100m, ["CSPX.L"] = 50m },
            inflationRates: new Dictionary<int, decimal>(),
            logger: logger);

        await service.BuildChartAsync(
            security, new DateOnly(2026, 1, 1), "USD", 10m, inflationAdjusted: true, new DateOnly(2026, 1, 7));

        Assert.Contains(logger.WarningMessages, m => m.Contains("USD") && m.Contains("2026"));
    }
}
