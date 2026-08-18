# Story: Current Portfolio Value Report

## Description
Show the current total portfolio value, broken down by position, in each security's own
currency and as a total converted to base currency.

## Acceptance Criteria
- [x] Report lists every current position (derived from transactions) with quantity,
      current price, value in security currency.
- [x] Each position's value is also shown converted to base currency using the latest
      valid FX rate.
- [x] A grand total in base currency is shown, summing all positions.
- [x] Positions with pending-validation prices/rates (story 07) are visually flagged and
      excluded from the total (or shown with an explicit caveat) rather than silently
      using unvalidated data.
- [x] Unit tests cover the aggregation/conversion math for a multi-currency portfolio.

## Technical Notes
- Position derivation (transactions → current holdings) is shared logic that later
  reports (10, 11) will also depend on — implement it as a reusable `Application`
  service, not embedded in this report's UI code.

## Dependencies / Open Questions
- Depends on [05-base-currency-setting](05-base-currency-setting.md) and
  [07-price-rate-quality-validation](07-price-rate-quality-validation.md).
