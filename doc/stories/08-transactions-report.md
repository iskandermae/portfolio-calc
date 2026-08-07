# Story: Transactions List Report

## Description
A report/window listing all transactions with sortable columns, where the chosen
sort/layout is remembered for future sessions.

## Acceptance Criteria
- [ ] A screen lists cash and security transactions together (or clearly tabbed), with
      key fields as columns (date, security, type, amount, currency, ...).
- [ ] Clicking a column header sorts by that field (asc/desc toggle).
- [ ] Column order/visibility and the current sort are saved locally and restored the
      next time the screen is opened.
- [ ] Integration test (or GUI test) confirms saved layout survives an app restart.

## Technical Notes
- Persist layout/sort state as a small local settings blob (e.g. JSON in the settings
  table introduced in story 05), keyed by this screen's identifier.

## Dependencies / Open Questions
- Depends on [01-transaction-data-storage](01-transaction-data-storage.md).
