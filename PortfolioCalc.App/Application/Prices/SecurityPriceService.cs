using PortfolioCalc.Core.Domain;
using PortfolioCalc.Core.Prices;
using PortfolioCalc.Core.Repositories;

namespace PortfolioCalc.App.Application.Prices;

/// <summary>Caches fetched security prices locally so the same security/date is never
/// fetched twice from an external provider — see
/// doc/stories/04-store-reuse-prices-rates.md. Mirrors <see
/// cref="PortfolioCalc.App.Application.Fx.FxRateService"/>; not yet wired into the app
/// because no concrete <see cref="ISecurityPriceProvider"/> exists (no price data source
/// has been chosen).</summary>
public class SecurityPriceService(ISecurityPriceRepository repository, ISecurityPriceProvider provider)
{
    public async Task<PriceResult> GetPriceAsync(
        Security security, DateOnly date, CancellationToken cancellationToken = default)
    {
        var stored = await repository.GetAsync(security.Id, date);
        if (stored is not null)
            return PriceResult.Ok(stored.Price);

        var fetched = await provider.GetPriceAsync(security.Symbol, security.Currency, date, cancellationToken);
        if (fetched.Status == PriceStatus.Success)
        {
            await repository.AddAsync(new SecurityPrice
            {
                SecurityId = security.Id,
                Date = date,
                Price = fetched.Price!.Value,
            });
        }

        return fetched;
    }

    public Task<IReadOnlyList<SecurityPrice>> GetHistoryAsync(int securityId, DateOnly from, DateOnly to) =>
        repository.GetRangeAsync(securityId, from, to);
}
