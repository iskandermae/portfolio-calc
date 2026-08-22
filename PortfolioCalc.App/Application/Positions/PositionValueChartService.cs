using Microsoft.Extensions.Logging;
using PortfolioCalc.App.Application.Fx;
using PortfolioCalc.App.Application.Inflation;
using PortfolioCalc.App.Application.Prices;
using PortfolioCalc.Core.Charting;
using PortfolioCalc.Core.Domain;
using PortfolioCalc.Core.Fx;
using PortfolioCalc.Core.Inflation;
using PortfolioCalc.Core.Prices;
using PortfolioCalc.Core.Repositories;

namespace PortfolioCalc.App.Application.Positions;

/// <summary>Builds the position-value chart for one security over a date range — see
/// doc/stories/10-position-value-chart-report.md. Orchestrates <see
/// cref="SecurityPriceService"/>, <see cref="FxRateService"/> and <see
/// cref="InflationRateService"/>; the actual sampling and inflation-adjustment math is pure
/// <see cref="Core.Charting"/> logic (directly unit-tested there).</summary>
public class PositionValueChartService(
    ISecurityRepository securityRepository,
    SecurityPriceService priceService,
    FxRateService fxRateService,
    InflationRateService inflationRateService,
    ILogger<PositionValueChartService> logger)
{
    /// <summary>Comparison series security — see the story's "equivalent investment in
    /// CSPX.L" paragraph.</summary>
    public const string ComparisonSymbol = "CSPX.L";

    /// <summary>Confirmed for real against Yahoo Finance's chart endpoint before writing
    /// this: CSPX.L's <c>meta.currency</c> comes back "USD", not GBP/GBp, even though it's
    /// LSE-listed — unlike VOD.L, no pence-to-pounds conversion applies here. See
    /// doc/decisions.md.</summary>
    private const string ComparisonCurrency = "USD";

    /// <summary>Same lookback window/reasoning as
    /// <see cref="PositionValuationService"/>'s own <c>AsOfLookbackDays</c> constant — see
    /// doc/decisions.md for why this is duplicated locally rather than extracted into a
    /// shared helper.</summary>
    private const int LookbackDays = 7;

    public async Task<PositionValueChartResult> BuildChartAsync(
        Security security,
        DateOnly startDate,
        string baseCurrency,
        decimal shares,
        bool inflationAdjusted,
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        var sampleDates = ChartDateSampler.GenerateSampleDates(startDate, today);
        var comparisonSecurity = await GetOrCreateComparisonSecurityAsync();
        var inflationRateCache = new Dictionary<int, decimal?>();
        // Dedupes gap log lines across repeated lookups for the same (security, date) or
        // (currency pair, date) — startDate is looked up once for the initial/comparison
        // amount and again as the first point of the sampled loop below, and a real gap on a
        // popular date would otherwise log twice per series.
        var loggedPriceGaps = new HashSet<(int SecurityId, DateOnly Date)>();
        var loggedFxGaps = new HashSet<(string From, string To, DateOnly Date)>();

        var initialAmount = await ComputeBaseCurrencyValueAsync(
            security, shares, startDate, baseCurrency, loggedPriceGaps, loggedFxGaps, cancellationToken);

        decimal? comparisonShares = null;
        if (initialAmount is not null)
        {
            var comparisonPricePerShare = await ComputeBaseCurrencyValueAsync(
                comparisonSecurity, 1m, startDate, baseCurrency, loggedPriceGaps, loggedFxGaps, cancellationToken);
            if (comparisonPricePerShare is > 0)
                comparisonShares = initialAmount / comparisonPricePerShare;
        }

        var primarySeries = new List<ChartPoint>();
        var comparisonSeries = new List<ChartPoint>();

        foreach (var date in sampleDates)
        {
            var primaryValue = await ComputeBaseCurrencyValueAsync(
                security, shares, date, baseCurrency, loggedPriceGaps, loggedFxGaps, cancellationToken);
            if (primaryValue is not null)
            {
                var adjusted = await ApplyInflationAsync(
                    primaryValue.Value, date, today, baseCurrency, inflationAdjusted, inflationRateCache, cancellationToken);
                if (adjusted is not null)
                    primarySeries.Add(new ChartPoint(date, adjusted.Value));
            }

            if (comparisonShares is not null)
            {
                var comparisonValue = await ComputeBaseCurrencyValueAsync(
                    comparisonSecurity, comparisonShares.Value, date, baseCurrency, loggedPriceGaps, loggedFxGaps, cancellationToken);
                if (comparisonValue is not null)
                {
                    var adjusted = await ApplyInflationAsync(
                        comparisonValue.Value, date, today, baseCurrency, inflationAdjusted, inflationRateCache, cancellationToken);
                    if (adjusted is not null)
                        comparisonSeries.Add(new ChartPoint(date, adjusted.Value));
                }
            }
        }

        logger.LogInformation(
            "Position value chart for {Symbol} ({Currency}) built: {PrimaryCount}/{SampleCount} primary points, " +
            "{ComparisonCount}/{SampleCount} comparison ({ComparisonSymbol}) points resolved.",
            security.Symbol, security.Currency, primarySeries.Count, sampleDates.Count,
            comparisonSeries.Count, sampleDates.Count, comparisonSecurity.Symbol);

        return new PositionValueChartResult(
            primarySeries, security.Symbol, comparisonSeries, comparisonSecurity.Symbol, baseCurrency);
    }

    /// <summary>Get-or-create the CSPX.L <see cref="Security"/> row. <see cref="Security.Exchange"/>
    /// is left null: the symbol already carries its own ".L" suffix, and a non-null exchange
    /// would make <c>YahooFinanceSecurityPriceProvider</c> try to append a second one from the
    /// exchange-suffix vocabulary.</summary>
    private async Task<Security> GetOrCreateComparisonSecurityAsync()
    {
        var existing = await securityRepository.GetBySymbolAndCurrencyAsync(ComparisonSymbol, ComparisonCurrency);
        if (existing is not null)
            return existing;

        return await securityRepository.AddAsync(new Security
        {
            Symbol = ComparisonSymbol,
            Name = "iShares Core S&P 500 UCITS ETF",
            Currency = ComparisonCurrency,
        });
    }

    /// <summary>Values <paramref name="quantity"/> shares of <paramref name="security"/> on
    /// <paramref name="date"/> (or the latest resolvable date within <see cref="LookbackDays"/>
    /// before it) and converts to <paramref name="baseCurrency"/> using the FX rate applicable
    /// on that same resolved date. Null if either the price or the conversion can't be
    /// resolved — a gap, per AC3, not an error — which is also logged (once per security/
    /// currency-pair and date) so it's visible on the Logs page instead of only showing up as
    /// a missing chart point.</summary>
    private async Task<decimal?> ComputeBaseCurrencyValueAsync(
        Security security, decimal quantity, DateOnly date, string baseCurrency,
        HashSet<(int SecurityId, DateOnly Date)> loggedPriceGaps,
        HashSet<(string From, string To, DateOnly Date)> loggedFxGaps,
        CancellationToken cancellationToken)
    {
        var priceResult = await FindLatestPriceAsync(security, date, cancellationToken);
        if (priceResult.Status != PriceStatus.Success)
        {
            if (loggedPriceGaps.Add((security.Id, date)))
            {
                logger.LogWarning(
                    "No price resolvable for {Symbol} ({Currency}) on or within {LookbackDays} days before " +
                    "{Date} — this chart point will be excluded. Reason: {Reason}",
                    security.Symbol, security.Currency, LookbackDays, date, priceResult.ErrorMessage);
            }
            return null;
        }

        var valueInSecurityCurrency = quantity * priceResult.Price!.Value;

        var fxResult = await FindLatestFxRateAsync(security.Currency, baseCurrency, date, cancellationToken);
        if (fxResult.Status != FxRateStatus.Success)
        {
            if (loggedFxGaps.Add((security.Currency, baseCurrency, date)))
            {
                logger.LogWarning(
                    "No FX rate resolvable for {FromCurrency}/{ToCurrency} on or within {LookbackDays} days " +
                    "before {Date} — this chart point will be excluded. Reason: {Reason}",
                    security.Currency, baseCurrency, LookbackDays, date, fxResult.ErrorMessage);
            }
            return null;
        }

        return valueInSecurityCurrency * fxResult.Rate!.Value;
    }

    /// <summary>Walks backwards from <paramref name="date"/> up to <see cref="LookbackDays"/>
    /// calendar days, returning the first <see cref="PriceStatus.Success"/> result — mirrors
    /// <c>PositionValuationService.FindLatestPriceAsync</c> (see doc/decisions.md). An
    /// unexpected exception from the underlying price provider (e.g. a malformed response for
    /// one specific date) is caught, logged, and treated as a failed lookup for that one date
    /// rather than aborting the whole chart build — a single bad historical date shouldn't
    /// take down the entire chart.</summary>
    private async Task<PriceResult> FindLatestPriceAsync(
        Security security, DateOnly date, CancellationToken cancellationToken)
    {
        PriceResult? lastResult = null;
        for (var offset = 0; offset < LookbackDays; offset++)
        {
            var lookupDate = date.AddDays(-offset);
            PriceResult result;
            try
            {
                result = await priceService.GetPriceAsync(security, lookupDate, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Price lookup for {Symbol} ({Currency}) on {LookupDate} threw an unexpected exception — " +
                    "treating it as a gap for this date instead of failing the whole chart.",
                    security.Symbol, security.Currency, lookupDate);
                result = PriceResult.NetworkFailure(
                    $"Unexpected error looking up {security.Symbol} price on {lookupDate:yyyy-MM-dd}: {ex.Message}");
            }

            if (result.Status == PriceStatus.Success)
                return result;
            lastResult = result;
        }

        return lastResult!;
    }

    /// <summary>Same backward-lookback shape (and exception resilience) as
    /// <see cref="FindLatestPriceAsync"/>, applied to FX rates instead of prices.</summary>
    private async Task<FxRateResult> FindLatestFxRateAsync(
        string fromCurrency, string toCurrency, DateOnly date, CancellationToken cancellationToken)
    {
        FxRateResult? lastResult = null;
        for (var offset = 0; offset < LookbackDays; offset++)
        {
            var lookupDate = date.AddDays(-offset);
            FxRateResult result;
            try
            {
                result = await fxRateService.GetRateAsync(fromCurrency, toCurrency, lookupDate, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "FX rate lookup for {FromCurrency}/{ToCurrency} on {LookupDate} threw an unexpected " +
                    "exception — treating it as a gap for this date instead of failing the whole chart.",
                    fromCurrency, toCurrency, lookupDate);
                result = FxRateResult.NetworkFailure(
                    $"Unexpected error looking up {fromCurrency}/{toCurrency} rate on {lookupDate:yyyy-MM-dd}: {ex.Message}");
            }

            if (result.Status == FxRateStatus.Success)
                return result;
            lastResult = result;
        }

        return lastResult!;
    }

    /// <summary>Applies the story's forward inflation adjustment from <paramref name="date"/>
    /// to <paramref name="today"/>, using <paramref name="baseCurrency"/>'s annual rates.
    /// Returns the unadjusted amount as-is (and never calls <see cref="InflationRateService"/>)
    /// when <paramref name="inflationAdjusted"/> is false. Returns null — a gap, not a crash —
    /// if any year's rate in the span can't be resolved; that gap is also logged as a
    /// warning so it's visible on the Logs page instead of only showing up as a missing
    /// chart point (per an explicit user request).</summary>
    private async Task<decimal?> ApplyInflationAsync(
        decimal baseCurrencyAmount,
        DateOnly date,
        DateOnly today,
        string baseCurrency,
        bool inflationAdjusted,
        Dictionary<int, decimal?> rateCache,
        CancellationToken cancellationToken)
    {
        if (!inflationAdjusted)
            return baseCurrencyAmount;

        for (var year = date.Year; year <= today.Year; year++)
        {
            if (rateCache.ContainsKey(year))
                continue;

            var result = await inflationRateService.GetRateAsync(baseCurrency, new DateOnly(year, 1, 1), cancellationToken);
            if (result.Status == InflationRateStatus.Success)
            {
                // InflationRate.Rate (and this result) is a percentage (e.g. 4.7 for 4.7%),
                // matching the source API's/InflationRateOverride vocabulary's convention —
                // the forward-adjustment formula needs a fraction (e.g. 0.047).
                rateCache[year] = result.Rate!.Value / 100m;
            }
            else
            {
                rateCache[year] = null;
                logger.LogWarning(
                    "No {BaseCurrency} inflation rate available for {Year} — chart points needing it will be " +
                    "excluded. Add an override on the Vocabularies page ({VocabularyType}, key \"{Key}\") to fill " +
                    "the gap. Reason: {Reason}",
                    baseCurrency, year, VocabularyTypes.InflationRateOverride,
                    $"{baseCurrency.ToUpperInvariant()}:{year}", result.ErrorMessage);
            }
        }

        var factor = InflationAdjustmentCalculator.ComputeForwardFactor(date, today, year => rateCache[year]);
        return factor is null ? null : baseCurrencyAmount * factor.Value;
    }
}
