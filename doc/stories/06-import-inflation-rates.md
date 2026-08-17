# Story: Import Inflation Rates

## Description
Import inflation rates per month/year for the base currency, on demand at calculation
time, so reports can present inflation-adjusted figures (used by stories 10 and 11).

## Acceptance Criteria
- [x] `Core` defines an inflation-rate model (base currency, period, rate) and a
      repository interface.
- [x] An import mechanism (source TBD — see open question) loads inflation rates into
      local storage, triggered on calculation demand rather than continuously.
- [x] Rates can be queried by period range for a given base currency.
- [x] Integration test covers importing and querying a sample set of rates.

## Technical Notes
- "On calculation demand" means: don't background-poll for inflation data; fetch/import
  it lazily when a report that needs it is actually requested and data is missing.

## Dependencies / Open Questions
- Depends on [05-base-currency-setting](05-base-currency-setting.md).
- **Open question — resolved:** the source is the World Bank API
  (api.worldbank.org, indicator `FP.CPI.TOTL.ZG`, annual CPI inflation), not a manual file
  import. It's free, needs no API key, and has no rate-limit tier to manage — matching this
  project's existing `FrankfurterFxRateProvider` pattern for external data (one small
  `Core` interface + one real `Data/` provider now). A manual-file importer would have
  forced the user to source and upload a file themselves every time they need a new
  period, working against this story's own "fetch/import lazily on demand" technical note;
  a real API can genuinely be queried on demand instead. See doc/decisions.md for how
  currency is mapped to the World Bank's country/region codes and how the period
  granularity is represented.
