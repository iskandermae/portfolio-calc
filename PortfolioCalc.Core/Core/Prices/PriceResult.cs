namespace PortfolioCalc.Core.Prices;

/// <summary>Result of an <see cref="ISecurityPriceProvider"/> lookup. <see cref="Price"/> is
/// only populated when <see cref="Status"/> is <see cref="PriceStatus.Success"/>.</summary>
public sealed record PriceResult(PriceStatus Status, decimal? Price, string? ErrorMessage = null)
{
    public static PriceResult Ok(decimal price) => new(PriceStatus.Success, price);

    public static PriceResult Unsupported(string errorMessage) =>
        new(PriceStatus.UnsupportedSecurity, null, errorMessage);

    public static PriceResult NetworkFailure(string errorMessage) =>
        new(PriceStatus.NetworkError, null, errorMessage);
}
