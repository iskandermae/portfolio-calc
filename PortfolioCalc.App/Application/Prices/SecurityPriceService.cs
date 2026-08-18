using PortfolioCalc.Core.Domain;
using PortfolioCalc.Core.Prices;
using PortfolioCalc.Core.Repositories;
using PortfolioCalc.Core.Validation;

namespace PortfolioCalc.App.Application.Prices;

/// <summary>Caches fetched security prices locally so the same security/date is never
/// fetched twice from an external provider — see
/// doc/stories/04-store-reuse-prices-rates.md. Mirrors <see
/// cref="PortfolioCalc.App.Application.Fx.FxRateService"/>. Newly-stored prices are checked
/// against recent history for statistical anomalies and excluded from normal reads until
/// reviewed — see doc/stories/07-price-rate-quality-validation.md.</summary>
public class SecurityPriceService(ISecurityPriceRepository repository, ISecurityPriceProvider provider)
{
    public async Task<PriceResult> GetPriceAsync(
        Security security, DateOnly date, CancellationToken cancellationToken = default)
    {
        var stored = await repository.GetAsync(security.Id, date);
        if (stored is not null)
        {
            // See FxRateService.GetRateAsync for why a pending/rejected row triggers a
            // live fetch instead of being returned or re-stored — doc/decisions.md.
            if (stored.Status == ValidationStatus.Valid)
                return PriceResult.Ok(stored.Price);

            return await provider.GetPriceAsync(security.Symbol, security.Currency, date, security.Exchange, cancellationToken);
        }

        var fetched = await provider.GetPriceAsync(security.Symbol, security.Currency, date, security.Exchange, cancellationToken);
        if (fetched.Status == PriceStatus.Success)
        {
            var status = await ClassifyAsync(security.Id, date, fetched.Price!.Value);
            await repository.AddAsync(new SecurityPrice
            {
                SecurityId = security.Id,
                Date = date,
                Price = fetched.Price!.Value,
                Status = status,
            });
        }

        return fetched;
    }

    public async Task<IReadOnlyList<SecurityPrice>> GetHistoryAsync(int securityId, DateOnly from, DateOnly to)
    {
        var history = await repository.GetRangeAsync(securityId, from, to);
        return history.Where(p => p.Status == ValidationStatus.Valid).ToList();
    }

    private async Task<ValidationStatus> ClassifyAsync(int securityId, DateOnly date, decimal candidate)
    {
        var windowStart = date.AddDays(-AnomalyDetector.TrailingWindowDays);
        var recent = await repository.GetRangeAsync(securityId, windowStart, date.AddDays(-1));
        var recentValid = recent.Where(p => p.Status == ValidationStatus.Valid).Select(p => p.Price).ToList();

        return AnomalyDetector.IsAnomalous(recentValid, candidate)
            ? ValidationStatus.PendingValidation
            : ValidationStatus.Valid;
    }
}
