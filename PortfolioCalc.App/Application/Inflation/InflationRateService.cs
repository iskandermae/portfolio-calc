using PortfolioCalc.Core.Charting;
using PortfolioCalc.Core.Domain;
using PortfolioCalc.Core.Inflation;
using PortfolioCalc.Core.Repositories;

namespace PortfolioCalc.App.Application.Inflation;

/// <summary>Caches fetched inflation rates locally so the same base currency/period is
/// never fetched twice from an external source, fetching lazily only when a caller actually
/// asks for a period that isn't stored yet — no background polling. Mirrors
/// <see cref="FxRateService"/> — see doc/stories/06-import-inflation-rates.md.</summary>
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

    /// <summary>Multiplier that expresses a <paramref name="fromDate"/>-priced amount in
    /// <paramref name="toDate"/>'s prices (see <see
    /// cref="InflationAdjustmentCalculator.ComputeForwardFactor"/>), resolving each year's rate
    /// via <see cref="GetRateAsync"/> (so a rate already stored/overridden is reused, not
    /// re-fetched). Null if any year's rate in the span can't be resolved — a caller-visible
    /// gap, per the same convention as the position-value chart (story 10).</summary>
    public async Task<decimal?> GetForwardFactorAsync(
        string baseCurrency, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
    {
        var ratesByYear = new Dictionary<int, decimal?>();
        for (var year = fromDate.Year; year <= toDate.Year; year++)
        {
            var result = await GetRateAsync(baseCurrency, new DateOnly(year, 1, 1), cancellationToken);
            // InflationRate.Rate is a percentage (e.g. 4.7 for 4.7%); the forward-adjustment
            // formula needs a fraction (e.g. 0.047).
            ratesByYear[year] = result.Status == InflationRateStatus.Success ? result.Rate!.Value / 100m : null;
        }

        return InflationAdjustmentCalculator.ComputeForwardFactor(fromDate, toDate, year => ratesByYear[year]);
    }
}
