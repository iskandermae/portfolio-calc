# Story: Price/FX Rate Quality Validation

## Description
Detect statistically unusual jumps in imported/fetched FX rates and security prices, and
require manual validation before such values are used in calculations — surfacing the
need for review as a status message on the main screen.

## Acceptance Criteria
- [x] Each incoming FX rate/price is compared against the recent historical
      volatility (stddev-based) of that same series; values outside an expected range are
      flagged as `PendingValidation` instead of `Valid`.
- [x] Calculations/reports only use values marked `Valid`; `PendingValidation` values are
      excluded from valuation and reports until reviewed.
- [x] The main screen shows a status message/badge whenever one or more values are
      pending validation, with a way to navigate to review them.
- [x] A review screen lists pending values and lets the user mark each Valid or reject/
      correct it.
- [x] Unit tests cover the stddev-based flagging logic with both normal and anomalous
      synthetic series.

## Technical Notes
- This validation state (`Valid` / `PendingValidation` / `Rejected`) applies to both the
  FX-rate and price series introduced in stories 03/04 — extend that schema rather than
  building a separate one.
- Threshold/window for the stddev comparison should be a tunable constant to start
  (not necessarily user-configurable in the MVP).

## Dependencies / Open Questions
- Depends on [04-store-reuse-prices-rates](04-store-reuse-prices-rates.md).
