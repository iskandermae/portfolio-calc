using PortfolioCalc.App.Application.Fx;
using PortfolioCalc.App.Application.Prices;
using PortfolioCalc.Core.Domain;
using PortfolioCalc.Core.Fx;
using PortfolioCalc.Core.Prices;
using PortfolioCalc.Core.Repositories;

namespace PortfolioCalc.App.Application.Positions;

/// <summary>Derives currently-held positions from transaction history and values them in
/// base currency — see doc/stories/09-portfolio-value-report.md. The position-derivation
/// half (<see cref="GetCurrentPositionsAsync"/>) is deliberately exposed as its own reusable
/// step per the story's Technical Note: later reports (10/11) will also need "what's
/// currently held" without necessarily wanting a valuation alongside it.</summary>
public class PositionValuationService(
    ISecurityTransactionRepository transactionRepository,
    SecurityPriceService priceService,
    BaseCurrencyConversionService baseCurrencyConversionService)
{
    /// <summary>Number of trailing calendar days tried, starting at the requested date and
    /// walking backwards, when looking for the latest available price/FX rate. Prices and FX
    /// rates are on-demand-fetched series with gaps (weekends, holidays, a source with no
    /// data yet for today) rather than a continuously-updated feed, so an exact-date lookup
    /// alone isn't reliable for "current value." A week comfortably spans any single holiday
    /// weekend without walking back so far the figure stops being "current." See
    /// doc/decisions.md.</summary>
    private const int AsOfLookbackDays = 7;

    /// <summary>Current net quantity per position (sum of Buy/TransferIn minus Sell
    /// quantities — Dividend/Tax don't carry a quantity), positions fully sold (net
    /// quantity == 0) excluded.</summary>
    public async Task<IReadOnlyList<HeldPosition>> GetCurrentPositionsAsync()
    {
        var transactions = await transactionRepository.GetAllAsync();

        return transactions
            .GroupBy(t => t.PositionId)
            .Select(g => new HeldPosition(g.First().Position!, NetQuantity(g)))
            .Where(h => h.Quantity > 0)
            .ToList();
    }

    /// <summary>Values every currently-held position as of <paramref name="asOf"/> (or the
    /// latest available date within <see cref="AsOfLookbackDays"/> before it), converts each
    /// to the current base currency, and sums the resolved ones into a grand total.</summary>
    public async Task<PortfolioValueReport> GetCurrentValueAsync(
        DateOnly asOf, CancellationToken cancellationToken = default)
    {
        var heldPositions = await GetCurrentPositionsAsync();
        var baseCurrency = await baseCurrencyConversionService.GetBaseCurrencyAsync();

        var valuations = new List<PositionValuation>();
        var grandTotal = 0m;

        foreach (var held in heldPositions)
        {
            var security = held.Position.Security!;
            var priceResult = await FindLatestPriceAsync(security, asOf, cancellationToken);

            decimal? price = null;
            decimal? valueInSecurityCurrency = null;
            decimal? valueInBaseCurrency = null;
            var resolved = false;

            if (priceResult.Status == PriceStatus.Success)
            {
                price = priceResult.Price!.Value;
                valueInSecurityCurrency = held.Quantity * price.Value;

                var conversion = await baseCurrencyConversionService.ConvertToBaseCurrencyAsync(
                    valueInSecurityCurrency.Value, security.Currency, asOf, cancellationToken);
                if (conversion.Status == FxRateStatus.Success)
                {
                    valueInBaseCurrency = conversion.Rate;
                    resolved = true;
                }
            }

            if (resolved)
                grandTotal += valueInBaseCurrency!.Value;

            valuations.Add(new PositionValuation(
                held.Position.Id,
                held.Position.Account!.Name,
                security.Symbol,
                security.Currency,
                held.Quantity,
                price,
                valueInSecurityCurrency,
                valueInBaseCurrency,
                resolved));
        }

        return new PortfolioValueReport(valuations, grandTotal, baseCurrency);
    }

    /// <summary>Tries <paramref name="asOf"/>, then walks backwards up to
    /// <see cref="AsOfLookbackDays"/> days, returning the first successful price found — or,
    /// if none of the attempted dates resolved, the last (most recent-date) failure, so the
    /// caller still has a status/message to surface.</summary>
    private async Task<PriceResult> FindLatestPriceAsync(
        Security security, DateOnly asOf, CancellationToken cancellationToken)
    {
        PriceResult? lastResult = null;
        for (var offset = 0; offset < AsOfLookbackDays; offset++)
        {
            var result = await priceService.GetPriceAsync(security, asOf.AddDays(-offset), cancellationToken);
            if (result.Status == PriceStatus.Success)
                return result;
            lastResult = result;
        }

        return lastResult!;
    }

    private static decimal NetQuantity(IEnumerable<SecurityTransaction> transactions) =>
        transactions.Sum(t => t.Type switch
        {
            SecurityTransactionType.Buy => t.Quantity!.Value,
            SecurityTransactionType.TransferIn => t.Quantity!.Value,
            SecurityTransactionType.Sell => -t.Quantity!.Value,
            _ => 0m,
        });
}
