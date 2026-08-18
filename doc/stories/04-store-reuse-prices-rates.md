# Story: Store and Reuse Fetched Prices and FX Rates

## Description
Persist fetched FX rates and security prices locally so they're reused instead of
re-fetched, reducing external calls and giving reports a stable historical series.

## Acceptance Criteria
- [x] `Core`/`Data` schema stores FX rates and security prices keyed by
      (currency pair or security, date).
- [x] Before fetching, the app checks local storage first; it only calls external
      providers (story 03, and a future security-price provider) for missing dates.
- [x] Historical series can be queried for a date range (needed by later report stories).
- [x] Integration tests verify a fetch-then-reuse flow makes no second external call for
      an already-stored date.

## Technical Notes
- This story generalizes the caching behavior for both FX rates and security prices —
  keep the storage schema/interface shared where the shapes match (date, value, source).
  Implemented as parallel `FxRate`/`SecurityPrice` entities + `IFxRateRepository`/
  `ISecurityPriceRepository`, each wrapped by an Application-layer caching service
  (`FxRateService`/`SecurityPriceService`) with the same check-then-fetch-then-store shape.

## Dependencies / Open Questions
- Depends on [03-fetch-cross-currency-rates](03-fetch-cross-currency-rates.md).
- **Resolved:** added `ISecurityPriceProvider` (+ `PriceResult`/`PriceStatus`), mirroring
  `IFxRateProvider`, and a `SecurityPriceService` caching orchestration against it.
- **Resolved (story 09 prerequisite):** the concrete `Data/` price provider is
  `YahooFinanceSecurityPriceProvider` (Yahoo Finance's public chart endpoint), now wired
  into `MauiProgram`/the GUI — see doc/decisions.md.
