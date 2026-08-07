# Story: Favorites List

## Description
A list of favorite shares/bonds/ETFs to quickly trigger reports for regular analysis.
Favorites include arbitrary securities the user chooses to watch, and automatically
include every security currently or previously held in the portfolio.

## Acceptance Criteria
- [ ] User can add/remove arbitrary securities (by ticker) to a favorites list, even if
      never held in the portfolio.
- [ ] Every security with at least one transaction is automatically included in the
      favorites list (no manual step needed).
- [ ] Favorites screen lets the user jump directly to the position chart (story 10) or
      performance report (story 11) for any listed security.

## Technical Notes
- Model favorites as a small local table of "watched" securities; portfolio-held
  securities are derived, not duplicated into the same table (avoid drift if a position
  is fully sold).

## Dependencies / Open Questions
- Depends on [01-transaction-data-storage](01-transaction-data-storage.md).
