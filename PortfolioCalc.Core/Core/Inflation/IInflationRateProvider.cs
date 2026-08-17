namespace PortfolioCalc.Core.Inflation;

/// <summary>Fetches the annual inflation rate for a base currency's representative
/// country/region, for the year containing the given period. Implementations live in
/// Data/Inflation/ (one per rate source), mirroring
/// <see cref="PortfolioCalc.Core.Fx.IFxRateProvider"/> — calling code depends only on this
/// interface (see doc/stories/06-import-inflation-rates.md).</summary>
public interface IInflationRateProvider
{
    Task<InflationRateResult> GetRateAsync(
        string baseCurrency,
        DateOnly period,
        CancellationToken cancellationToken = default);
}
