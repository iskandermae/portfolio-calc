# Story: Filters on Transactions and Portfolio Value Grids

## Description
Let the user narrow the Transactions grid (story 08) and the Current Portfolio Value grid
(story 09) by account and by currency. The Portfolio Value grid also gets the sortable
columns and column order/visibility layout the Transactions grid already has.

## Acceptance Criteria
- [x] Transactions page has an Account dropdown ("-- All accounts --" + every account) and a
      Currency dropdown ("-- All currencies --" + every currency present in the rows).
      Both are single-select and combine with each other and with the existing
      Primary/Secondary toggle.
- [x] Transactions currency filter matches the row's `Currency` column (the transaction's
      currency — equal to the security's currency for security transactions).
- [x] Portfolio Value page has the same two dropdowns; its currency filter matches the
      position's security currency.
- [x] Portfolio Value grand total is recomputed over the filtered (visible) positions only.
      Unresolved positions stay excluded from the total, as today.
- [x] Portfolio Value analytics (if shown) still work with a filter active; changing a
      filter does not re-fetch prices/FX/inflation.
- [x] Portfolio Value columns are sortable by clicking the header (asc/desc toggle), and
      column order/visibility can be changed, same behaviour as Transactions.
- [x] Portfolio Value sort and column order/visibility are saved locally and restored next
      time (new screen key in `UiLayoutSetting`).
- [x] Filter selections are NOT persisted on either page; both reset to "All" on open.
- [x] Unit tests cover the filtering logic and the filtered grand total.

## Technical Notes
- Follow the Tax report's account `<select>` pattern (`TaxEstimationReport.razor`).
- Reuse the Transactions page's column-state/saved-layout mechanism for Portfolio Value
  rather than duplicating it; extract only what both pages actually use (DRY, but no
  speculative generality).
- `PositionValuation` carries `AccountName` but no account id, so the Portfolio filter
  matches on account name (unique per account). Add an id only if names turn out not to be
  unique.
- Filtering/total logic with real behaviour (filtered grand total) goes in a testable
  non-UI class, not in `.razor` code.
- Dropdown options derive from the loaded data (accounts/currencies actually present), so a
  filter can never produce an always-empty result.
- Analytics columns on the Portfolio page (calculated, loaded on demand): sorting/layout
  applies to the base columns only. Don't add machinery to make analytics columns sortable;
  if it isn't trivial, leave them unsortable and report it.

## Implementation Notes
- Shared pieces: `Application/Grids/` (`AccountCurrencyFilter`, `GridLayout`), `Gui/Components/`
  (`AccountCurrencyFilterBar`, `ColumnChooser`); Portfolio filtering/sorting/total in
  `PortfolioValueGridView`. Tests in `PortfolioCalc.App.Tests`.
- Analytics columns on the Portfolio page are not sortable or reorderable (left as-is).
- Portfolio grand total is now a single right-aligned footer cell (columns are reorderable).

## Dependencies / Open Questions
- Depends on [08-transactions-report](08-transactions-report.md) and
  [09-portfolio-value-report](09-portfolio-value-report.md).
- Interpretation to confirm: "neither" persistence was chosen for filters, while layout/sort
  on Portfolio Value is persisted "as in transactions" — the story treats these as
  separate.
- Out of scope: security and date-range filters.
