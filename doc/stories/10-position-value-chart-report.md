# Story: Position Value Chart Report

## Description
For a selected position — including securities not currently held, per the favorites
list (story 12) — show a chart of its value over time, with an inflation-adjusted view.

## Acceptance Criteria
- [ ] User can select any security (owned position or favorite/watchlist entry) and see
      a chart of its value over the available historical price series.
- [ ] An inflation-adjustment toggle re-renders the series adjusted using the imported
      inflation rates (story 06) for the base currency.
- [ ] Chart handles securities with partial history gracefully (no crash on missing
      data points).

## Technical Notes
- Use the Blazor charting library selected for the project (e.g. Blazor-ApexCharts).
- Reuse the historical price series query from story 04 and the inflation query from
  story 06 rather than building new data-access paths.

## Dependencies / Open Questions
- Depends on [04-store-reuse-prices-rates](04-store-reuse-prices-rates.md),
  [06-import-inflation-rates](06-import-inflation-rates.md), and
  [12-favorites-list](12-favorites-list.md).
