# Story: Gross gain for the current tax year

## Description
New TAB. User enters:
- Start date of the current tax year — defaults to the most recent 6 April on or before
  today (inclusive: if today is 6 April, the period is today only — the prior tax year is
  already closed). A currency-specific default (e.g. 1 January for UAH) is deferred; the user
  sets the date manually if needed.
- A list of securities to sell today (grid: security from current positions + shares or total
  amount — whichever field is left blank is auto-recalculated from the security's last
  available price). A proposed sell quantity cannot exceed the security's currently held
  quantity.
- Tax base currency (USD, EUR, GBP, UAH — GBP by default). This is a separate global setting
  from the existing (reports 09–11) base currency, configured once on the Settings page and
  just displayed (read-only) here — not duplicated/editable per report.
- An account filter ("All accounts" or one specific account). All calculations — cost basis,
  actual/proposed sells, currently-held quantity — are scoped per Position (Account ×
  Security), never mixed across accounts even for the same security; the results grid has an
  Account column so this is visible even with the filter left on "All accounts".

User presses "Calculate".

## Acceptance Criteria
- [x] The report considers every actual Sell transaction within the current tax year (real
      sell price/date) plus the entered proposed sells (valued at each security's last
      available price today).
- [x] Cost basis (for both actual and proposed sells) uses the average-cost method across all
      of a position's Buy/TransferIn quantity — not FIFO/LIFO/specific-lot.
- [x] Grid columns, one row per security (actual + proposed sold quantity summed together):
      - Security
      - Security currency
      - Number of sold securities
      - Average buy cost (security currency)
      - Sell amount (security currency)
      - FX rate — buy: blended from each contributing Buy/TransferIn's own transaction-date
        rate (proportional to quantity), since average cost spans multiple historical buy
        dates/rates
      - FX rate — sell: blended from each sell leg's own rate (the actual sell date's rate for
        a real sell, today's rate for a proposed/simulated sell) — same blending as the buy
        side, since a security's actual and proposed sells can also span different dates
      - Average buy cost (tax base currency)
      - Sell amount (tax base currency)
      - Total gain (sell minus buy) in tax base currency, for the security
      - Total gain in tax base currency, for the whole report (footer)
- [x] A price or FX rate that can't be resolved for a historical (past) date stops the whole
      report calculation with an error — no partial results.
- [x] A price or FX rate that can't be resolved for today looks back up to 5 additional days
      for the last available value, logging a warning whenever that fallback is used.

## Technical Note
- UAH isn't covered by Frankfurter (the existing `IFxRateProvider`). Rates come from the
  National Bank of Ukraine's public statistics API instead:
  `https://bank.gov.ua/NBUStatService/v1/statdirectory/exchange?valcode={CODE}&date={yyyyMMdd}&json`
  (e.g. `valcode=EUR&date=20200302` for EUR on 2020-03-02). Needs a composite `IFxRateProvider`
  that delegates to this NBU source when either currency in the pair is UAH, and to
  `FrankfurterFxRateProvider` otherwise.
- The average-cost buy leg's base-currency conversion isn't a single FX lookup: each
  contributing Buy/TransferIn converts to base currency at its own transaction date's rate,
  and those converted amounts are summed (not the security-currency average converted once).

## Dependencies / Open Questions
- Reuse existing code for FX/price lookups and lookback where possible.
- Confirm UAH's FX pair coverage/format against the NBU API before implementation.
