using PortfolioCalc.Core.Domain;
using PortfolioCalc.Core.Fx;
using PortfolioCalc.Core.Repositories;

namespace PortfolioCalc.App.Application.Fx;

/// <summary>Converts an amount into the currently-configured base currency, reading the
/// setting and stored FX history fresh on every call — see
/// doc/stories/05-base-currency-setting.md. Changing the base currency later needs no data
/// migration: the next call simply resolves a different rate.</summary>
public class BaseCurrencyConversionService(IAppSettingsRepository settingsRepository, FxRateService fxRateService)
{
    /// <summary>Used when no base currency has been configured yet.</summary>
    public const string DefaultBaseCurrency = "USD";

    public async Task<string> GetBaseCurrencyAsync()
    {
        var settings = await settingsRepository.GetAsync();
        return settings?.BaseCurrency ?? DefaultBaseCurrency;
    }

    /// <summary>Converts <paramref name="amount"/> (in <paramref name="currency"/> on
    /// <paramref name="date"/>) into the current base currency. The result's
    /// <see cref="FxRateResult.Rate"/> field carries the converted amount, not a rate — see
    /// <see cref="FxRateService.GetRateAsync"/> for the same Status/payload shape this
    /// reuses.</summary>
    public async Task<FxRateResult> ConvertToBaseCurrencyAsync(
        decimal amount, string currency, DateOnly date, CancellationToken cancellationToken = default)
    {
        var baseCurrency = await GetBaseCurrencyAsync();
        var rateResult = await fxRateService.GetRateAsync(currency, baseCurrency, date, cancellationToken);
        return rateResult.Status == FxRateStatus.Success
            ? FxRateResult.Ok(amount * rateResult.Rate!.Value)
            : rateResult;
    }
}
