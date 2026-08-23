using Microsoft.Extensions.Logging;
using PortfolioCalc.App.Application.Fx;
using PortfolioCalc.App.Application.Inflation;
using PortfolioCalc.App.Application.Prices;
using PortfolioCalc.Core.Domain;
using PortfolioCalc.Core.Fx;
using PortfolioCalc.Core.Prices;
using PortfolioCalc.Core.Repositories;

namespace PortfolioCalc.App.Application.Positions;

/// <summary>Computes the Position TAB's additional analytics columns (net invested,
/// dividends, fees+taxes, cash-flow result) for one position from its full transaction
/// history — see doc/stories/11-position-performance-report.md.</summary>
public class PositionPerformanceService(
    ISecurityTransactionRepository transactionRepository,
    BaseCurrencyConversionService baseCurrencyConversionService,
    SecurityPriceService priceService,
    InflationRateService inflationRateService,
    ILogger<PositionPerformanceService> logger)
{
    /// <param name="currentValueInBaseCurrency">The position's current market value in base
    /// currency (e.g. <see cref="PositionValuation.ValueInBaseCurrency"/>, already computed by
    /// <see cref="PositionValuationService"/> for the same report row) — folded into <see
    /// cref="PositionPerformanceFigures.CashFlowResult"/> as-is (not inflation-adjusted, since
    /// it's already expressed in today's prices) so an open position's result reflects its
    /// unrealized value, not just realized cash flows. Null (e.g. price/FX unresolved) makes
    /// the result incomplete, same as any other unresolved contribution.</param>
    public async Task<PositionPerformanceFigures> GetPerformanceAsync(
        int positionId, decimal? currentValueInBaseCurrency, bool inflationAdjusted, DateOnly today,
        CancellationToken cancellationToken = default)
    {
        var transactions = await transactionRepository.GetByPositionAsync(positionId);
        var baseCurrency = await baseCurrencyConversionService.GetBaseCurrencyAsync();

        var totalInvested = 0m;
        var totalReturned = 0m;
        var totalDividends = 0m;
        var totalFeesAndTaxes = 0m;
        var cashFlowResult = 0m;
        var fullyResolved = true;

        foreach (var transaction in transactions)
        {
            var cashAmount = await ResolveCashAmountAsync(transaction, baseCurrency, cancellationToken);
            var feeAndTax = await ResolveFeeAndTaxAsync(transaction, baseCurrency, cancellationToken);
            if (cashAmount is null || feeAndTax is null)
            {
                fullyResolved = false;
                continue;
            }

            switch (transaction.Type)
            {
                case SecurityTransactionType.Buy:
                case SecurityTransactionType.TransferIn:
                    totalInvested += -cashAmount.Value;
                    break;
                case SecurityTransactionType.Sell:
                    totalReturned += cashAmount.Value;
                    break;
                case SecurityTransactionType.Dividend:
                    totalDividends += cashAmount.Value;
                    break;
            }

            totalFeesAndTaxes += feeAndTax.Value;

            var adjusted = await ApplyInflationAsync(
                cashAmount.Value + feeAndTax.Value, transaction.Date, today, baseCurrency, inflationAdjusted, cancellationToken);
            if (adjusted is null)
            {
                fullyResolved = false;
                continue;
            }
            cashFlowResult += adjusted.Value;
        }

        if (currentValueInBaseCurrency is not null)
            cashFlowResult += currentValueInBaseCurrency.Value;
        else
            fullyResolved = false;

        return new PositionPerformanceFigures(
            totalInvested - totalReturned, totalDividends, totalFeesAndTaxes, cashFlowResult, fullyResolved);
    }

    /// <summary>Resolves a transaction's own cash amount in base currency, keeping the domain's
    /// sign convention (negative = outflow, positive = inflow). A <see
    /// cref="SecurityTransactionType.TransferIn"/> carries no cash amount (see
    /// doc/decisions.md), so it's valued instead as the security's price on the transfer date
    /// — a synthetic negative "cost" — per this story's explicit business decision.</summary>
    private async Task<decimal?> ResolveCashAmountAsync(
        SecurityTransaction transaction, string baseCurrency, CancellationToken cancellationToken)
    {
        if (transaction.Type == SecurityTransactionType.TransferIn)
            return await ResolveTransferInCostAsync(transaction, baseCurrency, cancellationToken);

        if (transaction.Amount == 0)
            return 0m;

        var result = await baseCurrencyConversionService.ConvertToBaseCurrencyAsync(
            transaction.Amount, transaction.Currency, transaction.Date, cancellationToken);
        if (result.Status == FxRateStatus.Success)
            return result.Rate;

        logger.LogWarning(
            "Could not convert {Type} transaction {Id}'s amount to {BaseCurrency} on {Date} — excluding it from " +
            "this position's performance figures. Reason: {Reason}",
            transaction.Type, transaction.Id, baseCurrency, transaction.Date, result.ErrorMessage);
        return null;
    }

    private async Task<decimal?> ResolveTransferInCostAsync(
        SecurityTransaction transaction, string baseCurrency, CancellationToken cancellationToken)
    {
        var security = transaction.Position!.Security!;
        var priceResult = await priceService.GetPriceAsync(security, transaction.Date, cancellationToken);
        if (priceResult.Status != PriceStatus.Success)
        {
            logger.LogWarning(
                "No price resolvable for {Symbol} on transfer-in date {Date} — excluding transaction {Id} from " +
                "this position's performance figures. Reason: {Reason}",
                security.Symbol, transaction.Date, transaction.Id, priceResult.ErrorMessage);
            return null;
        }

        var costInSecurityCurrency = transaction.Quantity!.Value * priceResult.Price!.Value;
        var conversion = await baseCurrencyConversionService.ConvertToBaseCurrencyAsync(
            -costInSecurityCurrency, security.Currency, transaction.Date, cancellationToken);
        if (conversion.Status == FxRateStatus.Success)
            return conversion.Rate;

        logger.LogWarning(
            "Could not convert transfer-in transaction {Id}'s ({Symbol}) cost to {BaseCurrency} on {Date} — " +
            "excluding it from this position's performance figures. Reason: {Reason}",
            transaction.Id, security.Symbol, baseCurrency, transaction.Date, conversion.ErrorMessage);
        return null;
    }

    /// <summary>Sums <see cref="SecurityTransaction.FeeAmount"/> (its own currency) and <see
    /// cref="SecurityTransaction.TaxAmount"/> (the transaction's currency) in base currency.
    /// Zero (not null) when neither is set — most transactions have no fee/tax.</summary>
    private async Task<decimal?> ResolveFeeAndTaxAsync(
        SecurityTransaction transaction, string baseCurrency, CancellationToken cancellationToken)
    {
        var total = 0m;

        if (transaction.FeeAmount is not 0 and not null)
        {
            var result = await baseCurrencyConversionService.ConvertToBaseCurrencyAsync(
                transaction.FeeAmount.Value, transaction.FeeCurrency!, transaction.Date, cancellationToken);
            if (result.Status != FxRateStatus.Success)
            {
                logger.LogWarning(
                    "Could not convert transaction {Id}'s fee to {BaseCurrency} on {Date} — excluding it from " +
                    "this position's performance figures. Reason: {Reason}",
                    transaction.Id, baseCurrency, transaction.Date, result.ErrorMessage);
                return null;
            }
            total += result.Rate!.Value;
        }

        if (transaction.TaxAmount is not 0 and not null)
        {
            var result = await baseCurrencyConversionService.ConvertToBaseCurrencyAsync(
                transaction.TaxAmount.Value, transaction.Currency, transaction.Date, cancellationToken);
            if (result.Status != FxRateStatus.Success)
            {
                logger.LogWarning(
                    "Could not convert transaction {Id}'s tax to {BaseCurrency} on {Date} — excluding it from " +
                    "this position's performance figures. Reason: {Reason}",
                    transaction.Id, baseCurrency, transaction.Date, result.ErrorMessage);
                return null;
            }
            total += result.Rate!.Value;
        }

        return total;
    }

    /// <summary>Forward-adjusts <paramref name="amount"/> from <paramref name="from"/> to
    /// today's prices when <paramref name="inflationAdjusted"/> is set; returns it unchanged
    /// (0% adjustment) when it's not.</summary>
    private async Task<decimal?> ApplyInflationAsync(
        decimal amount, DateOnly from, DateOnly today, string baseCurrency, bool inflationAdjusted,
        CancellationToken cancellationToken)
    {
        if (!inflationAdjusted)
            return amount;

        var factor = await inflationRateService.GetForwardFactorAsync(baseCurrency, from, today, cancellationToken);
        if (factor is not null)
            return amount * factor.Value;

        logger.LogWarning(
            "No {BaseCurrency} inflation rate available to adjust a {Date} cash flow to today's prices — " +
            "excluding it from this position's cash-flow result.", baseCurrency, from);
        return null;
    }
}
