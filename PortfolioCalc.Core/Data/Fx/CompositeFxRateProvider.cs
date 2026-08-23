using PortfolioCalc.Core.Fx;

namespace PortfolioCalc.Core.Data.Fx;

/// <summary>Routes a UAH-involving pair to a dedicated provider (<see cref="NbuFxRateProvider"/>
/// in production — Frankfurter doesn't cover UAH) and everything else to a default provider
/// (<see cref="FrankfurterFxRateProvider"/> in production) — the per-currency composite
/// provider anticipated in doc/decisions.md. Takes both by interface, not concrete type, so
/// routing is unit-testable against fakes.</summary>
public sealed class CompositeFxRateProvider(
    IFxRateProvider defaultProvider, IFxRateProvider uahProvider) : IFxRateProvider
{
    private const string UahCode = "UAH";

    public Task<FxRateResult> GetRateAsync(
        string fromCurrency, string toCurrency, DateOnly date, CancellationToken cancellationToken = default)
    {
        var involvesUah =
            string.Equals(fromCurrency, UahCode, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(toCurrency, UahCode, StringComparison.OrdinalIgnoreCase);

        return involvesUah
            ? uahProvider.GetRateAsync(fromCurrency, toCurrency, date, cancellationToken)
            : defaultProvider.GetRateAsync(fromCurrency, toCurrency, date, cancellationToken);
    }
}
