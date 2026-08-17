using PortfolioCalc.Core.Domain;
using PortfolioCalc.Core.Inflation;
using PortfolioCalc.Core.Repositories;

namespace PortfolioCalc.App.Application.Inflation;

/// <summary>Caches fetched inflation rates locally so the same base currency/period is
/// never fetched twice from an external source, fetching lazily only when a caller actually
/// asks for a period that isn't stored yet — no background polling. Mirrors
/// <see cref="PortfolioCalc.App.Application.Fx.FxRateService"/> — see
/// doc/stories/06-import-inflation-rates.md.</summary>
public class InflationRateService(IInflationRateRepository repository, IInflationRateProvider provider)
{
    public async Task<InflationRateResult> GetRateAsync(
        string baseCurrency, DateOnly period, CancellationToken cancellationToken = default)
    {
        var stored = await repository.GetAsync(baseCurrency, period);
        if (stored is not null)
            return InflationRateResult.Ok(stored.Rate);

        var fetched = await provider.GetRateAsync(baseCurrency, period, cancellationToken);
        if (fetched.Status == InflationRateStatus.Success)
        {
            await repository.AddAsync(new InflationRate
            {
                BaseCurrency = baseCurrency,
                Period = period,
                Rate = fetched.Rate!.Value,
            });
        }

        return fetched;
    }

    public Task<IReadOnlyList<InflationRate>> GetHistoryAsync(
        string baseCurrency, DateOnly from, DateOnly to) =>
        repository.GetRangeAsync(baseCurrency, from, to);
}
