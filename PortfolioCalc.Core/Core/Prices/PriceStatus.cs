namespace PortfolioCalc.Core.Prices;

/// <summary>Outcome of an <see cref="ISecurityPriceProvider"/> fetch attempt — mirrors
/// <see cref="PortfolioCalc.Core.Fx.FxRateStatus"/> (see
/// doc/stories/04-store-reuse-prices-rates.md).</summary>
public enum PriceStatus
{
    Success,
    UnsupportedSecurity,
    NetworkError
}
