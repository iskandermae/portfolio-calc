using Microsoft.EntityFrameworkCore;
using PortfolioCalc.App.Application.Fx;
using PortfolioCalc.Core.Data;
using PortfolioCalc.Core.Data.Repositories;
using PortfolioCalc.Core.Domain;
using PortfolioCalc.Core.Fx;

namespace PortfolioCalc.App.Tests.Application.Fx;

public class BaseCurrencyConversionServiceTests
{
    private static readonly DateOnly KnownDate = new(2024, 1, 15);

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

    private sealed class FakeFxRateProvider : IFxRateProvider
    {
        public Task<FxRateResult> GetRateAsync(
            string fromCurrency, string toCurrency, DateOnly date, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "Test seeds stored FX history directly; the provider should never be called.");
    }

    [Fact]
    public async Task ConvertToBaseCurrencyAsync_recomputes_from_stored_fx_history_when_the_base_currency_changes()
    {
        using var context = CreateOpenInMemoryContext();
        var fxRateRepository = new FxRateRepository(context);
        var settingsRepository = new AppSettingsRepository(context);

        // Same stored FX history for the whole test — no data migration when the setting changes.
        await fxRateRepository.AddAsync(new FxRate
        {
            FromCurrency = "USD", ToCurrency = "EUR", Date = KnownDate, Rate = 0.9m,
        });
        await fxRateRepository.AddAsync(new FxRate
        {
            FromCurrency = "USD", ToCurrency = "GBP", Date = KnownDate, Rate = 0.8m,
        });

        var fxRateService = new FxRateService(fxRateRepository, new FakeFxRateProvider());
        var conversionService = new BaseCurrencyConversionService(settingsRepository, fxRateService);

        await settingsRepository.SaveAsync(new AppSettings { BaseCurrency = "EUR" });
        var eurResult = await conversionService.ConvertToBaseCurrencyAsync(100m, "USD", KnownDate);

        await settingsRepository.SaveAsync(new AppSettings { BaseCurrency = "GBP" });
        var gbpResult = await conversionService.ConvertToBaseCurrencyAsync(100m, "USD", KnownDate);

        Assert.Equal(FxRateStatus.Success, eurResult.Status);
        Assert.Equal(90m, eurResult.Rate);
        Assert.Equal(FxRateStatus.Success, gbpResult.Status);
        Assert.Equal(80m, gbpResult.Rate);
    }

    [Fact]
    public async Task GetBaseCurrencyAsync_falls_back_to_the_default_when_no_setting_has_been_saved()
    {
        using var context = CreateOpenInMemoryContext();
        var conversionService = new BaseCurrencyConversionService(
            new AppSettingsRepository(context), new FxRateService(new FxRateRepository(context), new FakeFxRateProvider()));

        Assert.Equal(BaseCurrencyConversionService.DefaultBaseCurrency, await conversionService.GetBaseCurrencyAsync());
    }
}
