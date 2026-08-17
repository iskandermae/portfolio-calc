# Story: Fetch Cross-Currency Rates

## Description
Fetch FX rates between a security's trading currency and the base currency from external
sources, so non-base-currency positions can be valued in base currency.

## Acceptance Criteria
- [x] `Core` defines an `IFxRateProvider` interface (currency pair + date → rate).
- [x] At least one concrete `Data/` implementation fetches a real rate for a real
      currency pair.
- [x] The provider design supports plugging in different sources per currency (e.g.
      different site/API for EUR vs. JPY) without changing calling code.
- [x] Failures (network error, unsupported currency) surface as a clear status rather
      than a silent gap in data.

## Technical Notes
- This story only covers *fetching*; local caching/reuse is story 04, and flagging
  suspicious values is story 07.
- Structure this as one small interface + one provider now; add more providers as
  separate follow-on work whenever a new currency needs a different source.

## Dependencies / Open Questions
- Depends on [00-solution-scaffolding](00-solution-scaffolding.md).
- **Open question:** which specific site(s)/API(s) to use per currency — decide when
  implementing, per currency, rather than upfront.
