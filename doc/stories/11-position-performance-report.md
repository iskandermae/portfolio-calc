# Story: Position Performance Report

## Description
For a selected position, show current price, all dividends, all expenses — with
inflation optionally considered — and the synthetic average yearly growth rate.

## Acceptance Criteria
- [ ] Report shows: current price, full list of dividends received, full list of
      expenses/fees, for the selected security.
- [ ] An inflation toggle in the UI adjusts the shown figures using imported inflation
      rates (story 06).
- [ ] The synthetic annual growth rate is computed by solving
      `currentPrice = initPrice * (1 + r)^n` on a days-elapsed basis, converted to an
      annual rate, and displayed.
- [ ] Unit tests cover the growth-rate solver against known inputs/outputs (including
      edge cases: very short holding periods, zero/negative growth).

## Technical Notes
- Implement the day-count-based solver as a standalone, well-tested `Core` function —
  it's pure math and the highest-risk-of-bugs piece of this story.

## Dependencies / Open Questions
- Depends on [06-import-inflation-rates](06-import-inflation-rates.md) and
  [09-portfolio-value-report](09-portfolio-value-report.md) (position derivation).
