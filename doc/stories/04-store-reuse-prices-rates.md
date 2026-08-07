# Story: Store and Reuse Fetched Prices and FX Rates

## Description
Persist fetched FX rates and security prices locally so they're reused instead of
re-fetched, reducing external calls and giving reports a stable historical series.

## Acceptance Criteria
- [ ] `Core`/`Data` schema stores FX rates and security prices keyed by
      (currency pair or security, date).
- [ ] Before fetching, the app checks local storage first; it only calls external
      providers (story 03, and a future security-price provider) for missing dates.
- [ ] Historical series can be queried for a date range (needed by later report stories).
- [ ] Integration tests verify a fetch-then-reuse flow makes no second external call for
      an already-stored date.

## Technical Notes
- This story generalizes the caching behavior for both FX rates and security prices —
  keep the storage schema/interface shared where the shapes match (date, value, source).

## Dependencies / Open Questions
- Depends on [03-fetch-cross-currency-rates](03-fetch-cross-currency-rates.md).
- **Open question:** the requirements doc doesn't name a source for security *prices*
  (only cross-currency rates) — confirm/add a price provider interface here, mirroring
  `IFxRateProvider`, when implementing.
