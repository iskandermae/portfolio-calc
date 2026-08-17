using PortfolioCalc.Core.Domain;
using PortfolioCalc.Core.Fx;
using PortfolioCalc.Core.Repositories;

namespace PortfolioCalc.App.Application.Fx;

/// <summary>Caches fetched FX rates locally so the same currency pair/date is never
/// fetched twice from an external provider — see
/// doc/stories/04-store-reuse-prices-rates.md.</summary>
public class FxRateService(IFxRateRepository repository, IFxRateProvider provider)
{
    public async Task<FxRateResult> GetRateAsync(
        string fromCurrency, string toCurrency, DateOnly date, CancellationToken cancellationToken = default)
    {
        if (string.Equals(fromCurrency, toCurrency, StringComparison.OrdinalIgnoreCase))
            return FxRateResult.Ok(1m);

        var stored = await repository.GetAsync(fromCurrency, toCurrency, date);
        if (stored is not null)
            return FxRateResult.Ok(stored.Rate);

        var fetched = await provider.GetRateAsync(fromCurrency, toCurrency, date, cancellationToken);
        if (fetched.Status == FxRateStatus.Success)
        {
            await repository.AddAsync(new FxRate
            {
                FromCurrency = fromCurrency,
                ToCurrency = toCurrency,
                Date = date,
                Rate = fetched.Rate!.Value,
            });
        }

        return fetched;
    }

    public Task<IReadOnlyList<FxRate>> GetHistoryAsync(
        string fromCurrency, string toCurrency, DateOnly from, DateOnly to) =>
        repository.GetRangeAsync(fromCurrency, toCurrency, from, to);
}
