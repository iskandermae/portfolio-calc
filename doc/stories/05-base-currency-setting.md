# Story: Base Currency Setting

## Description
Let the user choose which currency all portfolio totals and reports are converted to,
and ensure reports recompute correctly if this setting changes later.

## Acceptance Criteria
- [x] A settings screen/section lets the user pick a base currency from a supported list.
- [x] The setting is persisted locally and used by all valuation/report logic. (Scoped as
      a vertical slice — see Technical Notes: no report/valuation screens exist yet in this
      codebase, so this is satisfied by a real, narrow conversion capability those future
      screens will call, not by wiring it into report screens that don't exist.)
- [x] Changing the base currency after transactions/prices/FX history already exist does
      **not** require any data migration — reports simply recompute using stored FX
      history against the new base currency the next time they render.
- [x] Unit tests cover the recompute-on-change behavior (e.g. same portfolio, two
      different base currencies, produces correctly converted totals for both).

## Technical Notes
- Store the setting as a simple app-level config value (e.g. a single-row settings table
  or local config file) — no versioning/history needed for the setting itself. Implemented
  as a single-row `AppSettings` table (fixed `Id = 1`, upserted by
  `IAppSettingsRepository`/`AppSettingsRepository`), following the existing
  repository/DbContext pattern rather than a config file, for consistency with the rest of
  the app's storage.
- **Vertical-slice scoping**: stories 08–11 (the actual valuation/report screens) don't
  exist yet in this codebase, so AC #2 and the recompute test can't be built against a
  real report. Built instead: the base-currency *setting* (storage + GUI picker) plus a
  minimal Application-layer `BaseCurrencyConversionService` that reads the current setting
  and converts an amount in currency X on date D into the base currency via
  `FxRateService`/stored FX history. This is the narrow interface future report stories
  will call; no report/portfolio-total UI was built (out of scope, avoids
  pre-building CRUD/interfaces for stories not yet started, per CLAUDE.md).
- **Supported currency list**: no existing enum/list of currencies existed in the
  codebase. Assumed a small static list — USD, EUR, GBP, CHF, JPY, AUD, CAD
  (`SupportedCurrencies.Codes` in Core) — covering major currencies also supported by the
  Frankfurter FX provider (story 03/04), rather than a full ISO-4217 table. Extend only
  when a real need shows up.
- Default base currency when no setting has been saved yet: `USD`
  (`BaseCurrencyConversionService.DefaultBaseCurrency`).

## Dependencies / Open Questions
- Depends on [04-store-reuse-prices-rates](04-store-reuse-prices-rates.md) (needs FX
  history to convert into the newly chosen base currency). Satisfied.
