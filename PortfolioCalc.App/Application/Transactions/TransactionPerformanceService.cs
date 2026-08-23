using Microsoft.Extensions.Logging;
using PortfolioCalc.App.Application.Fx;
using PortfolioCalc.App.Application.Inflation;
using PortfolioCalc.App.Application.Prices;
using PortfolioCalc.Core.Analytics;
using PortfolioCalc.Core.Domain;
using PortfolioCalc.Core.Fx;
using PortfolioCalc.Core.Prices;

namespace PortfolioCalc.App.Application.Transactions;

/// <summary>Computes the Transaction TAB's additional analytics columns (CAGR, cash-flow
/// result) for a single Buy transaction — see
/// doc/stories/11-position-performance-report.md. Not applicable to Sell/Dividend/Tax/
/// TransferIn rows, per the story's explicit business decision.</summary>
public class TransactionPerformanceService(
    SecurityPriceService priceService,
    BaseCurrencyConversionService baseCurrencyConversionService,
    InflationRateService inflationRateService,
    ILogger<TransactionPerformanceService> logger)
{
    /// <summary>Same lookback window/reasoning as <see cref="Positions.PositionValuationService"/>'s
    /// own lookback constant — see doc/decisions.md.</summary>
    private const int PriceLookbackDays = 7;

    public async Task<BuyTransactionPerformance> GetBuyPerformanceAsync(
        SecurityTransaction transaction, bool inflationAdjusted, DateOnly today, CancellationToken cancellationToken = default)
    {
        if (transaction.Type != SecurityTransactionType.Buy)
            throw new ArgumentException("Only Buy transactions have a performance figure.", nameof(transaction));

        var security = transaction.Position!.Security!;
        var baseCurrency = await baseCurrencyConversionService.GetBaseCurrencyAsync();

        var initialConversion = await baseCurrencyConversionService.ConvertToBaseCurrencyAsync(
            -transaction.Amount, transaction.Currency, transaction.Date, cancellationToken);
        if (initialConversion.Status != FxRateStatus.Success)
        {
            logger.LogWarning(
                "Could not convert buy transaction {Id}'s ({Symbol}) initial investment to {BaseCurrency} on " +
                "{Date} — no CAGR/cash-flow result for this row. Reason: {Reason}",
                transaction.Id, security.Symbol, baseCurrency, transaction.Date, initialConversion.ErrorMessage);
            return new BuyTransactionPerformance(null, null);
        }
        var initialInvestment = initialConversion.Rate!.Value;

        var priceResult = await FindLatestPriceAsync(security, today, cancellationToken);
        if (priceResult.Status != PriceStatus.Success)
        {
            logger.LogWarning(
                "No price resolvable for {Symbol} on or within {LookbackDays} days before {Date} — no CAGR/" +
                "cash-flow result for buy transaction {Id}. Reason: {Reason}",
                security.Symbol, PriceLookbackDays, today, transaction.Id, priceResult.ErrorMessage);
            return new BuyTransactionPerformance(null, null);
        }

        var currentConversion = await baseCurrencyConversionService.ConvertToBaseCurrencyAsync(
            transaction.Quantity!.Value * priceResult.Price!.Value, security.Currency, today, cancellationToken);
        if (currentConversion.Status != FxRateStatus.Success)
        {
            logger.LogWarning(
                "Could not convert buy transaction {Id}'s ({Symbol}) current value to {BaseCurrency} — no CAGR/" +
                "cash-flow result for this row. Reason: {Reason}",
                transaction.Id, security.Symbol, baseCurrency, currentConversion.ErrorMessage);
            return new BuyTransactionPerformance(null, null);
        }
        var currentValue = currentConversion.Rate!.Value;

        var adjustedInitialInvestment = initialInvestment;
        if (inflationAdjusted)
        {
            var factor = await inflationRateService.GetForwardFactorAsync(
                baseCurrency, transaction.Date, today, cancellationToken);
            if (factor is null)
            {
                logger.LogWarning(
                    "No {BaseCurrency} inflation rate available to adjust buy transaction {Id}'s initial " +
                    "investment to today's prices — no CAGR/cash-flow result for this row.",
                    baseCurrency, transaction.Id);
                return new BuyTransactionPerformance(null, null);
            }
            adjustedInitialInvestment = initialInvestment * factor.Value;
        }

        var daysElapsed = today.DayNumber - transaction.Date.DayNumber;
        var cagr = GrowthRateCalculator.ComputeAnnualizedRate(adjustedInitialInvestment, currentValue, daysElapsed);
        var cashFlowResult = currentValue - adjustedInitialInvestment;

        return new BuyTransactionPerformance(cagr, cashFlowResult);
    }

    /// <summary>Same backward-lookback shape as <see
    /// cref="Positions.PositionValuationService"/>'s own price lookup — see doc/decisions.md
    /// for why this is duplicated locally rather than extracted into a shared helper.</summary>
    private async Task<PriceResult> FindLatestPriceAsync(
        Security security, DateOnly date, CancellationToken cancellationToken)
    {
        PriceResult? lastResult = null;
        for (var offset = 0; offset < PriceLookbackDays; offset++)
        {
            var result = await priceService.GetPriceAsync(security, date.AddDays(-offset), cancellationToken);
            if (result.Status == PriceStatus.Success)
                return result;
            lastResult = result;
        }
        return lastResult!;
    }
}
