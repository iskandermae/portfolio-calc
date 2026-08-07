# Story: Import Inflation Rates

## Description
Import inflation rates per month/year for the base currency, on demand at calculation
time, so reports can present inflation-adjusted figures (used by stories 10 and 11).

## Acceptance Criteria
- [ ] `Core` defines an inflation-rate model (base currency, period, rate) and a
      repository interface.
- [ ] An import mechanism (source TBD — see open question) loads inflation rates into
      local storage, triggered on calculation demand rather than continuously.
- [ ] Rates can be queried by period range for a given base currency.
- [ ] Integration test covers importing and querying a sample set of rates.

## Technical Notes
- "On calculation demand" means: don't background-poll for inflation data; fetch/import
  it lazily when a report that needs it is actually requested and data is missing.

## Dependencies / Open Questions
- Depends on [05-base-currency-setting](05-base-currency-setting.md).
- **Open question:** exact inflation data source (manual file import vs. an API) was
  deferred during planning — decide when implementing this story.
