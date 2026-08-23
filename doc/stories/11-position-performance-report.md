# Story: Position/Transaction Performance Figures

## Description
On Position and Transaction TABs show additional analytics columns, converted using
the existing global base-currency setting (no new per-report currency override):
- Show analytics [with additional setting: Inflation-adjusted (to today's prices)].
  The toggle is optional; when disabled, treat inflation adjustment as 0% (figures
  shown at nominal value). When enabled, it applies to every analytics column below,
  not just the cash-flow-result ones.

## Acceptance Criteria
- [x] Position TAB additional (calculated, optionally inflation-adjusted figures) columns:

      - Total invested amount (buy) minus total return (sell) in base currency. This
        is a net cash-flow total, not a valuation — it exists alongside the position's
        current market value (already shown elsewhere in the grid) so the user can see
        cost-to-get-this-position separately from current value. A `TransferIn` with no
        recorded price/cost is valued at the security's price on the transfer date.
      - Total dividends/coupons/in leu in base currency
      - Total Fees+taxes in base currency
      - Cash flow result for all transactions linked to a position in base currency (
            convert all amounts to base currency and add them inflation adjusted to today's prices)

- [x] Transaction TAB additional (calculated, optionally inflation-adjusted figures)
      columns, **buy transactions only** (Sell/Dividend/Tax/other rows leave these
      columns blank):

      - CAGR = (CurrentValue / InitialInvestment)^(1/Years) - 1, this works for
        fractional year also. Interpretation: the synthetic bank-deposit rate that
        would produce the same earning. To avoid extreme/misleading annualized values
        for very young transactions, floor the days-elapsed used in the exponent to
        10 days.
      - Cash flow result for buy transactions (current transaction only + current value of 
         the bought securities) — an absolute cash-flow amount, distinct from CAGR
         which expresses the equivalent annualized rate.

## Dependencies / Open Questions
- reuse existing code for inflation, cross-currency calculations.
