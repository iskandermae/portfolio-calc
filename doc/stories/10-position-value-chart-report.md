# Story: Position Value Chart Report

## Description
For a selected security show a chart of its value over time, with an inflation-adjusted view.
Let it be "weekly" data - four chart dates per month (but take monthly samples for the dates older than 1 year, just 1st of each month).
To avoid fetching too much data for FX rates and prices:
- For the most recent year, use the 1st, 7th, 14th, and 21st of each month.
- For dates older than one year (current date minus 1 year), fetch one value per month (1st).

There should be a possibility to setup: Security, start date of the period, base currency 
(default - current base currency from the settings), number of shares (default = 1), 
inflation adjustment flag (default value - On). Current date is the end of the period. 

Inflation adjustment means recalculating a historical value in base currency to express it 
in current prices (current date). This means we use forward adjustment. Example:
On 01 August 2024 the price was 130$
Inflation: 2024 - 4%, 2025 - 6%, 2026 - 5%
Adjusted price on 01 September 2026 = 
      130$ 
            × [ (1+4%) ^ (active days in 2024 / days in that year) ] 
            × [ (1+6%) ^ (active days in 2025 / days in that year) ] ... =
      $130
            × [(1 + 4%) ^ ((01.01.2025 - 01.08.2024) / 366)]
            × [(1 + 6%)]
            × [(1 + 5%) ^ ((01.09.2026 - 01.01.2026) / 365)]

Rules to build the chart:
For every chart date, calculate the value of the given number of shares, convert it to the base currency using the FX rate applicable on that date, and apply the inflation adjustment from that date to today.
Always include the selected start date as the first chart date. Calculate X using the price and FX rate applicable on that date.
Always include the current date as the final chart date.

If the inflation flag is turned off then we assume that inflation is equal to 0 for all years.

To avoid manual input of the chart parameters there should be a possibility to build the chart based on selected 
transaction - then security, start date, number of shares should be taken from the transaction.

The chart must include a second data series for comparison, representing an equivalent investment in CSPX.L.
Assume that CSPX.L was purchased instead of the selected security on the first chart date using the same initial amount. Calculate the fractional number of CSPX.L shares separately, using the CSPX.L price and applicable FX rate on that date.
Apply the same sampling, FX conversion, and inflation-adjustment rules to both data series. Both series must start at the same value, but each series uses its own independently calculated number of shares. 

## Acceptance Criteria
- [x] User can select any security (owned position or favorite/watchlist entry) and see
      a chart of its value over the available historical price series.
      **Partial**: story 12 (Favorites) doesn't exist yet, so there is no favorites/
      watchlist table to pick from. The security picker instead offers every security
      reachable from `ISecurityTransactionRepository.GetAllAsync()` (owned or previously
      held), plus a manual free-text Symbol+Currency entry for a security that's never
      been transacted — see doc/decisions.md. Owned/previously-held + manual-entry
      securities are fully supported; a true favorites list (saved for reuse without
      re-typing) is out of scope until story 12 lands.
- [x] An inflation-adjustment toggle re-renders the series adjusted using the imported
      inflation rates (story 06) for the base currency.
- [x] Chart handles securities with partial history gracefully (no crash on missing
      data points).

## Technical Notes
- Use the Blazor charting library selected for the project (e.g. Blazor-ApexCharts).
  Implemented with the `Blazor-ApexCharts-MAUI` NuGet package (the MAUI-specific variant,
  registered via `AddApexChartsMaui()`).
- Reuse the historical price series query from story 04 and the inflation query from
  story 06 rather than building new data-access paths. Implemented via
  `SecurityPriceService`/`FxRateService`/`InflationRateService`, orchestrated by the new
  `PositionValueChartService` (Application layer); sampling/inflation math lives in
  `PortfolioCalc.Core.Charting` (`ChartDateSampler`, `InflationAdjustmentCalculator`),
  unit-tested against the story's own worked example.

## Dependencies / Open Questions
- Depends on [04-store-reuse-prices-rates](04-store-reuse-prices-rates.md) and
  [06-import-inflation-rates](06-import-inflation-rates.md).
- Also listed [12-favorites-list](12-favorites-list.md) as a dependency, but story 12 is
  not implemented yet. Per an explicit user decision, this story does not add a
  favorites/watchlist table — see the AC1 note above and doc/decisions.md for the
  security-picker substitution used instead.
