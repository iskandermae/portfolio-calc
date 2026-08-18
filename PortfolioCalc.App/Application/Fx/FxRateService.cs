using PortfolioCalc.Core.Domain;
using PortfolioCalc.Core.Fx;
using PortfolioCalc.Core.Repositories;
using PortfolioCalc.Core.Validation;

namespace PortfolioCalc.App.Application.Fx;

/// <summary>Caches fetched FX rates locally so the same currency pair/date is never
/// fetched twice from an external provider — see
/// doc/stories/04-store-reuse-prices-rates.md. Newly-stored rates are checked against
/// recent history for statistical anomalies and excluded from normal reads until reviewed
/// — see doc/stories/07-price-rate-quality-validation.md.</summary>
public class FxRateService(IFxRateRepository repository, IFxRateProvider provider)
{
    public async Task<FxRateResult> GetRateAsync(
        string fromCurrency, string toCurrency, DateOnly date, CancellationToken cancellationToken = default)
    {
        if (string.Equals(fromCurrency, toCurrency, StringComparison.OrdinalIgnoreCase))
            return FxRateResult.Ok(1m);

        var stored = await repository.GetAsync(fromCurrency, toCurrency, date);
        if (stored is not null)
        {
            // A stored-but-not-yet-reviewed row isn't usable; nor is it re-fetched into
            // storage a second time (that would violate the pair/date unique index) — a
            // live fetch is attempted so calculations still get a usable rate, without
            // touching the row awaiting review. See doc/decisions.md.
            if (stored.Status == ValidationStatus.Valid)
                return FxRateResult.Ok(stored.Rate);

            return await provider.GetRateAsync(fromCurrency, toCurrency, date, cancellationToken);
        }

        var fetched = await provider.GetRateAsync(fromCurrency, toCurrency, date, cancellationToken);
        if (fetched.Status == FxRateStatus.Success)
        {
            var status = await ClassifyAsync(fromCurrency, toCurrency, date, fetched.Rate!.Value);
            await repository.AddAsync(new FxRate
            {
                FromCurrency = fromCurrency,
                ToCurrency = toCurrency,
                Date = date,
                Rate = fetched.Rate!.Value,
                Status = status,
            });
        }

        return fetched;
    }

    public async Task<IReadOnlyList<FxRate>> GetHistoryAsync(
        string fromCurrency, string toCurrency, DateOnly from, DateOnly to)
    {
        var history = await repository.GetRangeAsync(fromCurrency, toCurrency, from, to);
        return history.Where(r => r.Status == ValidationStatus.Valid).ToList();
    }

    private async Task<ValidationStatus> ClassifyAsync(
        string fromCurrency, string toCurrency, DateOnly date, decimal candidate)
    {
        var windowStart = date.AddDays(-AnomalyDetector.TrailingWindowDays);
        var recent = await repository.GetRangeAsync(fromCurrency, toCurrency, windowStart, date.AddDays(-1));
        var recentValid = recent.Where(r => r.Status == ValidationStatus.Valid).Select(r => r.Rate).ToList();

        return AnomalyDetector.IsAnomalous(recentValid, candidate)
            ? ValidationStatus.PendingValidation
            : ValidationStatus.Valid;
    }
}
