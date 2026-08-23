using Microsoft.Extensions.Logging;
using PortfolioCalc.App.Application.Fx;
using PortfolioCalc.App.Application.Prices;
using PortfolioCalc.Core.Analytics;
using PortfolioCalc.Core.Domain;
using PortfolioCalc.Core.Fx;
using PortfolioCalc.Core.Prices;
using PortfolioCalc.Core.Repositories;

namespace PortfolioCalc.App.Application.Tax;

/// <summary>Builds the gross-gain-for-the-current-tax-year report — see
/// doc/stories/12-tax-estimation-report.md. Cost basis is average-cost across every
/// Buy/TransferIn a security has ever had (not FIFO/LIFO/specific-lot); a historical
/// price/FX gap (a buy date, an actual sell date, a TransferIn date) stops the whole report
/// with a <see cref="TaxEstimationException"/>, while a gap for today's price/FX (used for a
/// proposed sell) falls back up to <see cref="TodayLookbackDays"/> days and is logged.</summary>
public class TaxEstimationService(
    ISecurityTransactionRepository transactionRepository,
    IAppSettingsRepository settingsRepository,
    SecurityPriceService priceService,
    FxRateService fxRateService,
    ILogger<TaxEstimationService> logger)
{
    /// <summary>Per this story's explicit business decision: only today's price/FX lookups
    /// (for a proposed sell) fall back to a recent day; a historical lookup never does.</summary>
    private const int TodayLookbackDays = 5;

    /// <summary>Falls back to <see cref="TaxSupportedCurrencies.DefaultCode"/> — logged,
    /// rather than silently — whenever no settings row exists yet, or an existing row's
    /// <see cref="AppSettings.TaxBaseCurrency"/> is blank (e.g. a pre-story-12 row, which the
    /// migration backfilled with an empty string rather than a real default).</summary>
    public async Task<string> GetTaxBaseCurrencyAsync()
    {
        var settings = await settingsRepository.GetAsync();
        if (!string.IsNullOrWhiteSpace(settings?.TaxBaseCurrency))
            return settings.TaxBaseCurrency;

        logger.LogWarning(
            "Tax base currency is not set — defaulting to {DefaultCurrency}. Set it on the Settings page.",
            TaxSupportedCurrencies.DefaultCode);
        return TaxSupportedCurrencies.DefaultCode;
    }

    /// <summary>The last available price for <paramref name="security"/> on or within <see
    /// cref="TodayLookbackDays"/> days before <paramref name="date"/> — used both by the Gui's
    /// share/amount auto-recalculation and by a proposed sell's valuation. Throws if none of
    /// those days resolve.</summary>
    public async Task<decimal> GetLastAvailablePriceAsync(
        Security security, DateOnly date, CancellationToken cancellationToken = default)
    {
        for (var offset = 0; offset < TodayLookbackDays; offset++)
        {
            var lookupDate = date.AddDays(-offset);
            var result = await priceService.GetPriceAsync(security, lookupDate, cancellationToken);
            if (result.Status == PriceStatus.Success)
            {
                if (offset > 0)
                {
                    logger.LogWarning(
                        "No price for {Symbol} on {Date} — used the {LookupDate} price instead.",
                        security.Symbol, date, lookupDate);
                }
                return result.Price!.Value;
            }
        }

        throw new TaxEstimationException(
            $"No price available for {security.Symbol} on {date:yyyy-MM-dd} or within " +
            $"{TodayLookbackDays} days before it.");
    }

    /// <summary>Currently held quantity for one specific position — never mixed with
    /// another account's holding of the same security (see doc/decisions.md); a proposed
    /// sell can't exceed this.</summary>
    public async Task<decimal> GetHeldQuantityAsync(int positionId, CancellationToken cancellationToken = default)
    {
        var transactions = await transactionRepository.GetAllAsync();
        return HeldQuantity(transactions.Where(t => t.PositionId == positionId));
    }

    /// <param name="accountIdFilter">When set, restricts the whole report (actual sells,
    /// proposed sells, and the cost basis they're matched against) to this one account's
    /// positions.</param>
    public async Task<TaxEstimationReport> ComputeAsync(
        DateOnly taxYearStart, IReadOnlyList<ProposedSell> proposedSells, DateOnly today,
        int? accountIdFilter = null, CancellationToken cancellationToken = default)
    {
        var baseCurrency = await GetTaxBaseCurrencyAsync();
        var allTransactions = await transactionRepository.GetAllAsync();
        if (accountIdFilter is not null)
            allTransactions = allTransactions.Where(t => t.Position!.AccountId == accountIdFilter.Value).ToList();

        // Grouped by Position (Account × Security), never aggregated across accounts — the
        // same security held in two accounts has two independent cost bases and two
        // independent "currently held" figures. See doc/decisions.md.
        var byPosition = allTransactions.GroupBy(t => t.PositionId).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var proposed in proposedSells)
        {
            var held = HeldQuantity(byPosition.GetValueOrDefault(proposed.PositionId, []));
            if (proposed.Quantity > held)
            {
                var symbol = byPosition.GetValueOrDefault(proposed.PositionId, [])
                    .FirstOrDefault()?.Position!.Security!.Symbol ?? $"position {proposed.PositionId}";
                throw new TaxEstimationException(
                    $"Cannot sell {proposed.Quantity} of {symbol}: only {held} currently held in that account.");
            }
        }

        var positionIds = byPosition.Keys.Union(proposedSells.Select(p => p.PositionId)).Distinct();
        var rows = new List<TaxEstimationRow>();

        foreach (var positionId in positionIds)
        {
            var transactions = byPosition.GetValueOrDefault(positionId, []);
            var position = transactions.FirstOrDefault()?.Position;
            var security = position?.Security ?? throw new TaxEstimationException($"Position {positionId} not found.");
            var accountName = position!.Account!.Name;

            var lots = new List<AverageCostCalculator.Lot>();
            foreach (var t in transactions.Where(
                t => t.Type is SecurityTransactionType.Buy or SecurityTransactionType.TransferIn))
            {
                lots.Add(await ResolveLotAsync(t, security, baseCurrency, cancellationToken));
            }
            var averageCost = AverageCostCalculator.Compute(lots);

            var sellLegs = new List<(decimal Quantity, decimal SecurityCurrency, decimal BaseCurrency)>();
            foreach (var sell in transactions.Where(
                t => t.Type == SecurityTransactionType.Sell && t.Date >= taxYearStart && t.Date <= today))
            {
                var baseAmount = await ConvertHistoricalAsync(
                    sell.Amount, sell.Currency, sell.Date, baseCurrency, cancellationToken);
                sellLegs.Add((sell.Quantity!.Value, sell.Amount, baseAmount));
            }

            var proposedSell = proposedSells.FirstOrDefault(p => p.PositionId == positionId);
            if (proposedSell is not null && proposedSell.Quantity > 0)
            {
                var price = await GetLastAvailablePriceAsync(security, today, cancellationToken);
                var securityCurrencyAmount = proposedSell.Quantity * price;
                var baseAmount = await ConvertTodayAsync(
                    securityCurrencyAmount, security.Currency, today, baseCurrency, cancellationToken);
                sellLegs.Add((proposedSell.Quantity, securityCurrencyAmount, baseAmount));
            }

            if (sellLegs.Count == 0)
                continue;

            if (averageCost is null)
            {
                throw new TaxEstimationException(
                    $"{security.Symbol} in {accountName} has no buy/transfer-in history — cannot compute a cost " +
                    $"basis for its sale.");
            }

            var quantitySold = sellLegs.Sum(l => l.Quantity);
            var sellSecurityCurrencyTotal = sellLegs.Sum(l => l.SecurityCurrency);
            var sellBaseCurrencyTotal = sellLegs.Sum(l => l.BaseCurrency);
            var buySecurityCurrencyTotal = quantitySold * averageCost.PerShareInSecurityCurrency;
            var buyBaseCurrencyTotal = quantitySold * averageCost.PerShareInBaseCurrency;

            rows.Add(new TaxEstimationRow(
                accountName,
                security.Symbol,
                security.Currency,
                quantitySold,
                buySecurityCurrencyTotal,
                sellSecurityCurrencyTotal,
                BlendedRate(buyBaseCurrencyTotal, buySecurityCurrencyTotal),
                BlendedRate(sellBaseCurrencyTotal, sellSecurityCurrencyTotal),
                buyBaseCurrencyTotal,
                sellBaseCurrencyTotal,
                sellBaseCurrencyTotal - buyBaseCurrencyTotal));
        }

        return new TaxEstimationReport(rows, rows.Sum(r => r.GainInBaseCurrency), baseCurrency);
    }

    private static decimal BlendedRate(decimal baseCurrencyAmount, decimal securityCurrencyAmount) =>
        securityCurrencyAmount == 0 ? 0 : baseCurrencyAmount / securityCurrencyAmount;

    private static decimal HeldQuantity(IEnumerable<SecurityTransaction> transactions) =>
        transactions.Sum(t => t.Type switch
        {
            SecurityTransactionType.Buy => t.Quantity!.Value,
            SecurityTransactionType.TransferIn => t.Quantity!.Value,
            SecurityTransactionType.Sell => -t.Quantity!.Value,
            _ => 0m,
        });

    /// <summary>A Buy converts at its own transaction-date rate; a TransferIn (no cash
    /// amount, see doc/decisions.md) is costed as quantity × the security's price on the
    /// transfer date, same as story 11's `PositionPerformanceService`.</summary>
    private async Task<AverageCostCalculator.Lot> ResolveLotAsync(
        SecurityTransaction transaction, Security security, string baseCurrency, CancellationToken cancellationToken)
    {
        if (transaction.Type == SecurityTransactionType.TransferIn)
        {
            var priceResult = await priceService.GetPriceAsync(security, transaction.Date, cancellationToken);
            if (priceResult.Status != PriceStatus.Success)
            {
                throw new TaxEstimationException(
                    $"No price available for {security.Symbol} on {transaction.Date:yyyy-MM-dd} (transfer-in " +
                    $"transaction {transaction.Id}) — cannot compute its cost basis. {priceResult.ErrorMessage}");
            }

            var costInSecurityCurrency = transaction.Quantity!.Value * priceResult.Price!.Value;
            var costInBaseCurrency = await ConvertHistoricalAsync(
                costInSecurityCurrency, security.Currency, transaction.Date, baseCurrency, cancellationToken);
            return new AverageCostCalculator.Lot(transaction.Quantity!.Value, costInSecurityCurrency, costInBaseCurrency);
        }

        // Buy.Amount is negative (cash outflow, see doc/decisions.md); cost is the magnitude.
        var buyCostInSecurityCurrency = -transaction.Amount;
        var buyCostInBaseCurrency = await ConvertHistoricalAsync(
            buyCostInSecurityCurrency, transaction.Currency, transaction.Date, baseCurrency, cancellationToken);
        return new AverageCostCalculator.Lot(transaction.Quantity!.Value, buyCostInSecurityCurrency, buyCostInBaseCurrency);
    }

    private async Task<decimal> ConvertHistoricalAsync(
        decimal amount, string fromCurrency, DateOnly date, string toCurrency, CancellationToken cancellationToken)
    {
        var result = await fxRateService.GetRateAsync(fromCurrency, toCurrency, date, cancellationToken);
        if (result.Status != FxRateStatus.Success)
        {
            throw new TaxEstimationException(
                $"No {fromCurrency}/{toCurrency} FX rate available for {date:yyyy-MM-dd} — cannot complete the " +
                $"tax estimation report. {result.ErrorMessage}");
        }
        return amount * result.Rate!.Value;
    }

    private async Task<decimal> ConvertTodayAsync(
        decimal amount, string fromCurrency, DateOnly today, string toCurrency, CancellationToken cancellationToken)
    {
        for (var offset = 0; offset < TodayLookbackDays; offset++)
        {
            var lookupDate = today.AddDays(-offset);
            var result = await fxRateService.GetRateAsync(fromCurrency, toCurrency, lookupDate, cancellationToken);
            if (result.Status == FxRateStatus.Success)
            {
                if (offset > 0)
                {
                    logger.LogWarning(
                        "No {FromCurrency}/{ToCurrency} FX rate for {Date} — used the {LookupDate} rate instead.",
                        fromCurrency, toCurrency, today, lookupDate);
                }
                return amount * result.Rate!.Value;
            }
        }

        throw new TaxEstimationException(
            $"No {fromCurrency}/{toCurrency} FX rate available for {today:yyyy-MM-dd} or within " +
            $"{TodayLookbackDays} days before it.");
    }
}
