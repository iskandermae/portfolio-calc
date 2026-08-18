# Story: Transactions List Report

## Description
A report/window listing all transactions with sortable columns, where the chosen
sort/layout is remembered for future sessions.

## Acceptance Criteria
- [x] A screen lists cash and security transactions together (or clearly tabbed), with
      key fields as columns (date, security, type, amount, currency, ...).
- [x] Clicking a column header sorts by that field (asc/desc toggle).
- [x] Column order/visibility and the current sort are saved locally and restored the
      next time the screen is opened.
- [x] Integration test (or GUI test) confirms saved layout survives an app restart.

## Technical Notes
- Persist layout/sort state as a small local settings blob (e.g. JSON in the settings
  table introduced in story 05), keyed by this screen's identifier.

## Dependencies / Open Questions
- Depends on [01-transaction-data-storage](01-transaction-data-storage.md).

## Later enhancement (not a numbered story)
- A default "Primary" (Buy/Sell/Deposit/TransferIn/Withdrawal) filter, with a toggle to
  reveal "Secondary"/income transactions (Tax/Interest/Dividend), was added on top of
  this story — see `TransactionCategory`/`TransactionCategoryClassifier` and
  doc/decisions.md. The toggle is persisted in the same `SavedLayout` blob this story
  introduced. Amount/Fee/Quantity/Date formatting was also tightened (N2, right-aligned,
  `yyyy-MM-dd`) as part of the same pass.
