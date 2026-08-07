# Story: Base Currency Setting

## Description
Let the user choose which currency all portfolio totals and reports are converted to,
and ensure reports recompute correctly if this setting changes later.

## Acceptance Criteria
- [ ] A settings screen/section lets the user pick a base currency from a supported list.
- [ ] The setting is persisted locally and used by all valuation/report logic.
- [ ] Changing the base currency after transactions/prices/FX history already exist does
      **not** require any data migration — reports simply recompute using stored FX
      history against the new base currency the next time they render.
- [ ] Unit tests cover the recompute-on-change behavior (e.g. same portfolio, two
      different base currencies, produces correctly converted totals for both).

## Technical Notes
- Store the setting as a simple app-level config value (e.g. a single-row settings table
  or local config file) — no versioning/history needed for the setting itself.

## Dependencies / Open Questions
- Depends on [04-store-reuse-prices-rates](04-store-reuse-prices-rates.md) (needs FX
  history to convert into the newly chosen base currency).
