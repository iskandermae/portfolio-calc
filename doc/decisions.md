# Decisions

Why the domain model looks the way it does. Short entries, why only — the what is in the
code. Add an entry here whenever a non-obvious design choice is made; don't let it drift.

- **`Position` (Account × Security) exists, not just `Security`.** The user holds the same
  security across multiple brokerage accounts and needs them tracked separately, not
  aggregated by ticker alone. Any "by security" query/report must aggregate across all of
  a security's `Position`s, not assume one row per security.
- **`SecurityTransaction` references `Position`, not `Security` directly.** A buy/sell/
  dividend always happens within one account's holding of a security, never against the
  security in the abstract.
- **`CashTransaction` has no link to `Position`, only `Account`.** Cash movements are
  account-level, not tied to any specific holding.
- **Dividends/coupons are `SecurityTransaction`s, not `CashTransaction`s.** They're tied
  to a specific position and occur on distinct dates (e.g. quarterly), not generic
  account cash flow.
- **Fee is a field on each transaction, not its own transaction type.** A fee is
  incidental to another economic event (a buy, a withdrawal) and can be in a different
  currency than that event.
- **Transaction-type enums are intentionally minimal.** Extend only when a real case
  shows up — don't pre-model kinds that aren't needed yet.
- **Repository interfaces are split per aggregate, not one combined interface.** Keeps
  each interface cohesive as query methods accumulate over time.
- **Import target format is IBKR Flex Query XML, not Activity Statement CSV.** Flex Query
  has a stable, flat per-row schema designed for automated consumption; Activity
  Statement is a multi-section human report with repeating headers, harder to parse
  reliably. Confirmed against a real sample export.
- **`Security` is keyed by `Symbol` + `Currency`, not by ISIN.** The same symbol can be
  quoted in more than one currency (e.g. a dual-listed ETF), and each currency-denominated
  listing is a distinct tradeable instrument. ISIN is deliberately not imported or stored
  for now — reintroduce only if a real case shows up that `Symbol` + `Currency` can't
  disambiguate.
- **Account is resolved/auto-created by the export's account alias, falling back to the
  raw account id.** The alias is the human-meaningful, stable label; the raw id is a
  fallback only for exports that omit it. No GUI account-picker step for this story.
- **Sign convention: incoming/profit amounts are positive, outgoing/cost amounts are
  negative** (e.g. a Buy's cash impact is negative, a Sell's is positive, a fee is always
  negative). This lets amounts be summed directly for cash-flow totals without branching
  on transaction type. The importer preserves whatever sign the source file already
  reports (IBKR reports commission as negative) rather than normalizing to a magnitude —
  a broker's own sign is authoritative and shouldn't be second-guessed or discarded.
- **`SecurityTransaction.TaxAmount` is a field separate from `FeeAmount`, not a reuse of
  it.** A dividend's withholding tax and a trade's broker commission are unrelated costs
  that happen to both reduce cash; conflating them would make it impossible to later tell
  which cost was which when reading a transaction back.
- **Dedup key is `Account + Security + Amount + Date + Currency`, applied after mapping
  to the domain model, not to raw import rows.** Some domain transactions (e.g. a
  dividend) are themselves aggregated from multiple raw rows, so dedup can only run once
  that aggregation has happened. No dedicated dedup schema (external id/source column) —
  the natural key is enough to make re-importing the same export a no-op.
- **A standalone `Tax`-type transaction exists for withholding tax with no dividend to
  attach to** (e.g. a tax-rate correction on a prior period, reported alone). Without it,
  such tax events would have nowhere to go.
- **`TransferIn` exists for in-kind security transfers into an account (no cash impact).**
  `TransferOut` is deliberately not added — no real case for it has shown up yet (YAGNI).
- **`Interest` (cash transaction type) exists for broker-paid interest income.** It's
  account-level income with no security/position, and conceptually distinct from a
  `Deposit` (an external cash movement, not income earned by the account).
- **Dividend/withholding-tax rows are grouped and summed by (Security, Date) rather than
  matched one-to-one.** A dividend and its tax can be reported as more than one row each
  (e.g. an original entry plus a same-day rate-correction restatement); summing with sign
  preserved avoids having to guess which row is "the real one."
- **IBKR's currency-conversion "trades" are recognized and explicitly skipped, not
  imported, and kept distinct from "unrecognized" rows.** They're a real, understood
  shape (mechanics of settling a multi-currency trade), not a security trade and not a
  parsing failure — conflating them with genuinely unrecognized rows would hide actual
  parsing problems.
- **The point-in-time position snapshot in an export is never read.** It's not a
  transaction log; importing it would duplicate information already derivable from the
  transaction history and risks contradicting it.
- **The import parser produces raw rows only — no enum mapping, aggregation, or
  account/security resolution.** That's real domain logic and belongs in the Application
  layer per the project's Core/Data/Application boundary, not in the broker-specific
  parser.
- **The import integration test project links the Application layer's source files
  directly instead of referencing the App project.** The App project's MAUI build
  tooling fails when referenced from a plain test project; the Application layer itself
  has no MAUI dependency, so this sidesteps a build-tool conflict without weakening the
  architecture boundary (the Gui layer stays untested by this project).
- **Broker-specific import code lives in its own `Ibkr/` subfolder under each layer's
  `Import/` folder** (`Core/Import/Ibkr/`, `Data/Import/Ibkr/`, `Application/Import/Ibkr/`,
  mirrored in the test project), with IBKR's own literal strings centralized in one
  `IbkrConstants` file rather than repeated across the mapping code. Only the
  broker-agnostic pieces (`ITransactionImporter`, `ImportResult`/`SkippedRow`) stay at the
  `Import/` root. A second broker's importer should follow the same shape — its own
  sibling subfolder and constants file — so brokers never bleed into each other's code.
- **The app uses EF Core migrations (`Database.Migrate()`), not `EnsureCreated()`, to
  bring up its local database.** `EnsureCreated()` only creates tables when none exist —
  it silently leaves an already-existing database's schema untouched, so any schema
  change (e.g. the `Symbol` rename, `TaxAmount`) never reaches a user's existing local
  database and the next write fails with a raw SQLite column error. Every model change
  from now on needs a matching migration (`dotnet ef migrations add <Name> --project
  PortfolioCalc.Core --output-dir Data/Migrations`) — `PortfolioDbContextFactory` exists
  purely so that command can run against the Core class library without needing the MAUI
  app as the EF startup project. `EnsureCreated()` is still fine (and used) in tests
  against ephemeral in-memory databases, where there's no prior schema to diverge from.
- **A `--reset-db` command-line flag deletes the local database before startup**, as an
  escape hatch for a database that's beyond what a migration can fix. It's a flag on the
  app itself, not a "go delete this file by hand" instruction, so recovering doesn't
  require knowing where the file lives or using a shell.
- **`IFxRateProvider` fetches one currency pair for one date; no batch/range method.**
  Calling code only ever needs "this pair, this date" (a position valuation), so a range API
  would be speculative. Different sources per currency (e.g. a different provider for a
  currency Frankfurter doesn't cover) can be added later behind a composite that also
  implements `IFxRateProvider` — callers stay unaware either way, since they only ever
  depend on the interface.
- **`FrankfurterFxRateProvider` (frankfurter.dev, ECB reference rates) is the first/only
  `IFxRateProvider` implementation.** Free, no API key, no rate limit tier to manage — good
  enough for a single-user local app. It doesn't cover every currency a broker export might
  report; that surfaces as `FxRateStatus.UnsupportedCurrency` per call rather than being
  worked around preemptively with a second provider before a real currency needs one.
- **`FxRateResult` carries a `FxRateStatus` (`Success`/`UnsupportedCurrency`/`NetworkError`)
  instead of throwing or returning a nullable rate.** A network failure and an unsupported
  currency are both real, distinguishable outcomes a caller needs to react to differently
  (retry vs. skip), not exceptions to catch or a silent gap to notice later.
- **`ITransactionImporter.ParseAsync` parses via an async `XmlReader` +
  `XDocument.LoadAsync`, never `XDocument.Load(Stream)`.** The GUI hands the importer a
  Blazor `InputFile` stream over the browser/WebView bridge, whose synchronous `Read()`
  throws `"Synchronous reads are not supported."` — `XDocument.Load(Stream)` reads
  synchronously internally, so it works against `File.OpenRead()` in a test but breaks
  against the real GUI file stream. Caught by a test using a stream that mimics the
  browser stream's read behavior (throws on sync `Read`, only `ReadAsync` works) — a
  fully-seekable, sync-capable `FileStream` in a test can't surface this class of bug at
  all, no matter how thoroughly it's otherwise exercised.
- **`FxRate` and `SecurityPrice` are separate entities/repositories, not one combined
  table.** Their shapes match (date, value) but their keys don't (currency pair vs.
  security id) — splitting per aggregate follows the existing repository-interface
  convention rather than forcing a shared schema with unused columns for either side.
  The *caching* logic is what's actually shared: `FxRateService`/`SecurityPriceService`
  (Application layer) both do check-repository → fetch-from-provider-if-missing →
  store-if-successful, as parallel implementations rather than a generic base — two
  call sites didn't justify the complexity of a shared generic abstraction.
- **`ISecurityPriceProvider` (+ `PriceResult`/`PriceStatus`) exists with no concrete
  `Data/` implementation yet.** No price data source has been chosen (unlike FX, which
  has Frankfurter from story 03); the interface and its `SecurityPriceService` caching
  wrapper were still built now, mirroring `IFxRateProvider`, so the storage schema and
  caching shape are already in place for whichever provider a future story adds — that
  story only needs to supply the `Data/` implementation and DI wiring, tested here
  against a fake provider instead of a real API.
- **The base-currency setting is a single-row `AppSettings` table (fixed `Id = 1`,
  upserted), not a local config file.** Keeps it in the same SQLite database and behind
  the same repository pattern as everything else, instead of introducing a second
  persistence mechanism for one value.
- **Base-currency conversion (story 05) is a narrow `BaseCurrencyConversionService`
  Application-layer method, not wired into any report/portfolio-total screen.** Stories
  08–11 (the actual report screens) don't exist yet, so there's nothing real to wire it
  into; the service reads the setting and stored FX history fresh on every call — proving
  no migration is needed when the setting changes — and is the interface those future
  screens will call.
- **`SupportedCurrencies.Codes` is a small static list (USD, EUR, GBP, CHF, JPY, AUD,
  CAD), not a full ISO-4217 table.** No currency enum/list existed before story 05; these
  are major currencies also covered by the Frankfurter provider (story 03/04). Extend only
  when a real currency need shows up.
- **Inflation data source (story 06's deferred open question) is the World Bank API, not
  manual file import.** It's free, no API key, no rate-limit tier — same reasoning as
  Frankfurter for FX (story 03) — and, unlike a manual import, a real API can genuinely be
  queried lazily on calculation demand rather than requiring the user to source and upload
  a file themselves each time a new period is needed.
- **`InflationRate.Period` is a `DateOnly` normalized to the 1st of the year (the World
  Bank's CPI indicator is annual-only), not a bespoke year/month "period" type.** Reuses
  the same comparable/range-queryable shape as `FxRate.Date`/`SecurityPrice.Date` instead
  of inventing a new period abstraction for what is just a queryable key; a future monthly
  source would normalize to the 1st of its month the same way.
- **`IInflationRateProvider` takes a base currency, not a country code.** The World Bank
  indexes data by country/region, so `WorldBankInflationRateProvider` privately maps each
  of `SupportedCurrencies.Codes` to one representative country/region code (e.g. EUR to
  the World Bank's "EMU" euro-area aggregate) — callers stay in currency terms, matching
  every other rate/price interface in this codebase, and the mapping is an implementation
  detail of this one provider rather than a general currency-to-country concept.
- **`FxRate`/`SecurityPrice` gain a `ValidationStatus` column (`Valid`/`PendingValidation`/
  `Rejected`, `Valid = 0`) rather than a separate validation table** — story 07 extends the
  existing schema per its own Technical Note. The migration defaults existing rows to
  `Valid` (the enum's zero value): pre-story-07 rows were already being used unreviewed, and
  there's no recent-history context left to judge them against retroactively, so leaving
  them trusted is the only defensible backfill.
- **The stddev anomaly check (`AnomalyDetector` in `Core/Validation/`) is a pure static
  function taking a history list and a candidate value, kept independent of the DB/service
  layer** — mirrors the general principle (story 11's day-count solver call-out) of
  isolating pure calc logic so it's directly unit-testable against synthetic series.
  Constants: `MinHistoryPoints = 5` (fewer points can't produce a meaningful stddev, so a
  new value is never flagged against too-thin a baseline — including a series' very first
  value); `ThresholdStdDevs = 3.0` (the conventional outlier cutoff, ~99.7% of normally
  distributed values fall within 3 stddevs — catches real jumps without flagging ordinary
  day-to-day volatility); `TrailingWindowDays = 90` (a few months of history is enough to
  characterize current volatility without diluting it with a stale, possibly
  different-regime past). A perfectly flat history (stddev 0) flags any change at all —
  the only sane interpretation when there's no observed variation to compare against.
- **`FxRateService`/`SecurityPriceService` classify a newly-fetched value using only the
  *valid* rows in its trailing window** (pending/rejected rows already in that window are
  excluded from the mean/stddev calculation) — an unreviewed or bad prior value shouldn't
  be allowed to normalize the baseline that judges the next one.
- **When the only stored row for an exact date is `PendingValidation`/`Rejected`,
  `GetRateAsync`/`GetPriceAsync` neither return it nor re-insert a second row for that
  date** (the unique pair/date and security/date indexes would reject a duplicate) — they
  fetch straight from the live provider and return that instead, leaving the stored row
  untouched for manual review. This keeps a calculation usable in the meantime without
  silently trusting an unreviewed value or failing outright. `GetHistoryAsync` on both
  services filters to `Valid` rows, since a stored range read (unlike the live-fetch path)
  has no live fallback to reach for.
- **Rejecting/correcting a pending value reuses the same status field and row** —
  `IFxRateRepository`/`ISecurityPriceRepository.UpdateStatusAsync(id, status,
  correctedValue?)` sets the status and optionally overwrites the stored value in one call,
  rather than modeling a correction as a new row or a separate audit table. This story only
  needs "accept as-is, correct the value, or reject" — no history of what a value used to
  be before correction, so there's nothing yet to justify a separate audit trail.
- **The validation-review page (`ValidationReview.razor`) calls `IFxRateRepository`/
  `ISecurityPriceRepository` directly, not through an Application service** — listing
  pending rows and updating a status/value is plain CRUD per CLAUDE.md's Gui/Application
  boundary; the actual logic (the stddev classification) lives in the services, not here.
- **Saved screen layouts (column order/visibility, sort) live in their own
  `UiLayoutSetting` table (`ScreenKey` + `LayoutJson`), not in the `AppSettings` singleton
  row from story 05.** `AppSettings` is deliberately a fixed `Id = 1` row for one global
  setting (base currency); a per-screen layout is keyed by screen identifier and there can
  be many screens, so it needs its own key rather than overloading the singleton row or
  adding screen-specific columns to it. `TransactionsReport.razor` (story 08) reads/writes
  it directly, same CRUD-through-Gui reasoning as `ValidationReview.razor` above — no
  Application-layer logic involved in saving a layout blob.
- **`ISecurityTransactionRepository.GetAllAsync`/`ICashTransactionRepository.GetAllAsync`
  eager-load `Position.Account`/`Position.Security` and `Account` respectively** — the
  transactions list report (story 08) needs the account name and security symbol as
  display columns for every row, and a report screen listing everything is the first real
  caller that needs "all rows," unlike the existing range/security/account-scoped
  queries.
- **`YahooFinanceSecurityPriceProvider` (Yahoo Finance's public, undocumented "chart"
  endpoint — the same one `yfinance` wraps) is the first/only `ISecurityPriceProvider`
  implementation**, closing the open question left by story 04. Chosen over Stooq's CSV
  quote endpoint: Stooq requires a real, working exchange-suffix mapping from this app's
  plain `Symbol` to a Stooq ticker (e.g. `.US`) before it can be trusted for anything beyond
  US tickers, whereas Yahoo's plain `Security.Symbol` already works unmodified for
  US-listed tickers (this app's common case, since transactions are imported from a US
  broker) with no mapping step to get wrong. The endpoint requires a browser-like
  `User-Agent` header (Yahoo's edge otherwise returns 429 on the very first request — a bot
  filter, not a rate limit) and returns a structured JSON error body even on a 404 status,
  so the response body is always parsed before checking the status code. Confirmed against
  real tickers (AAPL, SPY) and a nonexistent one before committing.
- **Non-US listings and any symbol/currency Yahoo doesn't recognize are a per-call
  `PriceStatus.UnsupportedSecurity`, not worked around with a mapping table up front** —
  mirrors the `FrankfurterFxRateProvider`/`WorldBankInflationRateProvider` precedent of
  accepting a real, known coverage gap rather than pre-building for exchanges no story
  needs priced yet.
- **One specific instance of that gap is handled rather than left unsupported: Yahoo quotes
  London-listed securities in pence (`meta.currency` = `"GBp"`), not pounds.** A request for
  a security whose `Currency` is `GBP` (already in `SupportedCurrencies.Codes`, and a pair
  `FrankfurterFxRateProvider` already prices against USD/EUR) against a pence-quoted symbol
  converts the close price ÷100 on the fly instead of being rejected as a currency mismatch
  — worth the small amount of special-casing because GBP is a currency this app already
  models end-to-end (base-currency picker, FX conversion), unlike an arbitrary unmapped
  exchange suffix. The comparison distinguishing `"GBp"` from `"GBP"` is ordinal/case-sensitive
  on purpose — Yahoo's two codes differ only by case and mean different things (pence vs.
  pounds), so a case-insensitive check would silently misprice a plain GBP-quoted security by
  100x. Confirmed for real against VOD.L before writing the conversion.
- **`PositionValuationService` (Application layer) derives current holdings and values
  them in base currency for the portfolio value report (story 09).** Position derivation
  (`GetCurrentPositionsAsync`) is exposed as its own public step, separate from valuation,
  per the story's Technical Note that later reports (10/11) will need "what's currently
  held" without necessarily wanting a valuation alongside it — net quantity per position is
  Buy/TransferIn minus Sell quantities (Dividend/Tax never carry a quantity), and a fully
  sold position (net quantity == 0) is dropped rather than shown at zero.
- **"Current price"/"latest valid FX rate" for the portfolio value report resolves by
  walking backwards up to 7 calendar days from the requested as-of date, using the first
  `Success` result.** Prices and FX rates are on-demand-fetched series with real gaps
  (weekends, holidays, a source with no data yet for "today"), and `SecurityPriceService`/
  `FxRateService` only expose exact-date lookups — a general "as-of" resolution system
  wasn't needed, just enough lookback to clear a single holiday weekend without walking
  back so far the figure stops being "current." No caller existed yet that needed this, so
  it lives in `PositionValuationService`, not as a new method on the two services.
- **`Security.Exchange` stores the broker's raw listing-exchange code (e.g. IBKR's
  "LSEETF", "IBIS"), imported from any raw row that resolves a Security** (Trade,
  the dividend/tax grouping pass, Transfer) — not just Trade rows, since a future export
  may report it more broadly (confirmed real need: the user's own export already carries
  it on CashTransaction rows too). A newly-created Security gets it directly; an
  already-existing Security with no Exchange recorded (e.g. imported before this field
  existed) is backfilled the next time a row for it is seen — the first non-empty value
  ever observed is authoritative and later rows never overwrite it, since IBKR isn't
  expected to report conflicting exchanges for the same Symbol+Currency and silently
  flip-flopping on it would be worse than just keeping the first answer.
- **A generic `VocabularyEntry` table (`VocabularyType` + `Key` + `Value` + `Description`),
  not a hard-coded static mapping, backs the exchange → Yahoo-suffix lookup** (superseding
  an earlier hard-coded-dictionary approach) — the user asked for several such lookup
  tables to be user-editable via a Gui CRUD page rather than requiring a code change each
  time a new exchange/vocabulary shows up. One table with a `VocabularyType`
  discriminator (see `VocabularyTypes`) avoids a new schema/migration per vocabulary; a
  missing key and an empty `Value` are both valid, distinct "no suffix" outcomes from
  "there is no data at all" (unmapped code → not present as a row). Seeded via
  `HasData` with ARCA/NASDAQ/NYSE → "" (US, no suffix), LSEETF/LSE → ".L" (London Stock
  Exchange), IBIS → ".DE" (Xetra/Deutsche Börse) — the exchanges seen in the user's own
  sample export. An unmapped/unknown code (or none at all) falls back to the plain
  symbol, same as before this feature — an honest "we don't know this market yet" gap,
  not guessed further.
- **The Vocabularies Gui page also exposes read-only "Security Prices"/"FX Rates" sub-tabs**
  (via new `ISecurityPriceRepository.GetAllAsync`/`IFxRateRepository.GetAllAsync`
  methods) alongside the CRUD-able vocabulary sub-tabs, per an explicit user request to
  see stored prices/rates from the same screen — plain repository reads called directly
  from the Gui, same CRUD boundary as `ValidationReview.razor`.
- **The Transactions report's type filter is modeled as a `TransactionCategory`
  (`Primary`/`Secondary`) classification, not a per-type checkbox list.** Every current
  `CashTransactionType`/`SecurityTransactionType` is classified in one place
  (`TransactionCategoryClassifier`) and an unclassified new type throws rather than
  silently vanishing from both views. Buy/Sell/Deposit/TransferIn/Withdrawal are
  `Primary` (shown by default); Tax/Interest/Dividend are `Secondary` (income/fee noise,
  hidden by default, revealed by one toggle). The toggle is persisted in the same
  `SavedLayout` JSON blob the screen already saves (a new field defaults to `false` when
  absent from already-saved JSON, keeping old saves compatible).
- **App-wide logging is a small custom `ILoggerProvider`/`ILogger` (`FileLoggerProvider`)
  appending timestamped lines to `log.txt` in `PortfolioDbContext.DefaultDataDirectory`,
  not a third-party logging library.** The user explicitly asked to avoid silent
  failures with nothing to look at; `Microsoft.Extensions.Logging` ships no file
  provider out of the box, and the app's actual need (append line, read it back on a Gui
  page, let the user clear it) is small enough that a ~40-line provider is proportionate
  — no rotation, log levels UI, or structured viewer. Its `IsEnabled` only accepts
  `Warning` and above, not `Information` — this provider is registered app-wide (not
  scoped to one category), so at `Information` it also captured Entity Framework Core's
  own verbose per-query SQL tracing, drowning the file in benign query dumps instead of
  showing the real page-load errors it exists for. Discovered by inspecting a real
  `log.txt` after building this feature — an `Error`-level `Logger.LogError(...)` call
  still passes through fine at the `Warning` threshold.
- **`PortfolioValueReport.razor`/`TransactionsReport.razor`/`Import.razor`'s data-loading
  now catches exceptions, logs via injected `ILogger<T>`, and shows a friendly message
  instead of leaving the page blank.** `Home.razor`/`Settings.razor`/`ValidationReview.razor`
  were deliberately left as-is: they only read simple repository state (pending lists,
  the settings row), not the external-API-calling services (`SecurityPriceService`/
  `FxRateService`/`InflationRateService`/`BaseCurrencyConversionService`) that can
  realistically fail — adding the same wrapper there would be defensive scaffolding
  with nothing real behind it yet.
- **Story 10 (position value chart) has no favorites table to pick a security from, since
  story 12 (Favorites) isn't implemented yet.** Per explicit user decision, the security
  picker instead builds its dropdown from every distinct `Position.Security` reachable via
  `ISecurityTransactionRepository.GetAllAsync()` (owned or previously held — no new
  repository method added, per CLAUDE.md's no-speculative-methods rule), plus a manual
  free-text Symbol+Currency entry (get-or-create via `ISecurityRepository`, same pattern
  IBKR import already uses) for a security that's never been transacted. A standalone
  `PositionChart.razor` page holds this parameter form (security, start date, base
  currency defaulting to the current base-currency setting, shares defaulting to 1,
  inflation toggle defaulting on); `TransactionsReport.razor` also gained a per-row
  "Chart" button that navigates there prefilled (security id, transaction date as start
  date, transaction quantity as shares) via query string, covering the story's "build from
  a selected transaction" requirement without needing story 12 at all.
- **The chart's date-sampling boundary ("dates older than one year") is evaluated
  per-candidate-date against a fixed cutoff of `today.AddYears(-1)`, using strict `<`.**
  A candidate date strictly before the cutoff is coarsened to only its month's 1st; a
  candidate on or after the cutoff (including one falling exactly on it) keeps the full
  1st/7th/14th/21st sampling for its month. "Older than" reads as strict in the story's
  wording, so the cutoff date itself is deliberately not coarsened. Covered by
  `ChartDateSamplerTests` (`Core/Charting/ChartDateSampler.cs`), including the exact-cutoff
  boundary.
- **CSPX.L's real trading currency was confirmed against Yahoo Finance's live chart
  endpoint before hard-coding it: `meta.currency` comes back `"USD"`, not GBP/GBp**, even
  though it's LSE-listed (unlike VOD.L, which really is pence-quoted) — so
  `PositionValueChartService`'s comparison series requests/creates the CSPX.L `Security`
  row with `Currency = "USD"` and no pence conversion. Its `Exchange` is left null: the
  `Symbol` already carries Yahoo's own ".L" suffix, and a non-null exchange would make
  `YahooFinanceSecurityPriceProvider` try to append a second suffix from the
  exchange-suffix vocabulary.
- **The backward price/FX lookback helper (`AsOfLookbackDays`-style walk, see
  `PositionValuationService`) is duplicated locally in `PositionValueChartService` rather
  than extracted into a shared helper.** Two call sites doesn't justify a shared
  abstraction, per the existing `FxRateService`/`SecurityPriceService` caching precedent
  above — consistent with that established call, not a new one.
- **A sample date's per-year inflation rate, and its price/FX-rate lookup, that can't be
  resolved (even after the 7-day lookback / a genuinely unpublished year's inflation
  figure) drops that one sample point from that series instead of throwing or defaulting
  to zero.** Mirrors AC3 ("no crash on missing data points"), extended from
  price/FX-only to inflation data too, since the same kind of gap (a source with no data
  yet) applies to all three inputs. If the *start date* itself can't be resolved for the
  primary security, the CSPX.L comparison series has no initial amount to size its
  fractional share count from and is left empty for that build, rather than guessing an
  initial value.
- **A missing inflation rate for a year (chart or otherwise) is logged as a warning, and can
  be filled in by hand via a new "InflationRateOverride" `VocabularyEntry` type.** Per an
  explicit user request after the chart feature above shipped: `PositionValueChartService`
  now logs `LogWarning` (base currency, year, and the exact Vocabularies key to add) whenever
  a year's rate can't be resolved, instead of the gap only showing up indirectly as a missing
  chart point. `VocabularyOverrideInflationRateProvider` (Data layer) wraps
  `WorldBankInflationRateProvider`: it only consults the vocabulary when the wrapped call
  fails (never shadowing a real published rate), keyed `"{baseCurrency}:{year}"` (e.g.
  "USD:2026"), with the value stored as a percentage number to match `InflationRate.Rate`'s
  existing convention (e.g. "5.2" for 5.2%) rather than a fraction. Registered in
  `MauiProgram.cs` by splitting the `IInflationRateProvider` DI registration: the real
  `WorldBankInflationRateProvider` is registered as itself (still via `AddHttpClient` for its
  `HttpClient`), and `IInflationRateProvider` resolves to the wrapping decorator — the same
  "composite provider" shape already anticipated for FX in this file. `Vocabularies.razor`
  always offers this vocabulary as a tab (unlike other vocabulary types, which only appear
  once a row exists) since a user must be able to add the very first override before any row
  exists yet — the CRUD is otherwise 100% the page's existing generic Key/Value/Description
  table, no new UI code.
- **`InflationRate.Rate`'s percentage convention (e.g. 4.7 for 4.7%) — already documented on
  the entity itself — was not being converted before being fed into
  `InflationAdjustmentCalculator.ComputeForwardFactor`, which expects a fraction (e.g.
  0.047).** Caught and fixed while wiring up the logging/override change above:
  `PositionValueChartService.ApplyInflationAsync` now divides by 100 when caching a
  successfully-resolved rate. This was a real bug in the original story 10 implementation,
  not a pre-existing issue — flagged here since the pure `Core.Charting` unit tests (which
  pass fractions directly) couldn't have caught it.
- **`YahooFinanceSecurityPriceProvider` parses `indicators`/`quote`/`close` defensively
  (`TryGetProperty` at every step) instead of a direct `GetProperty` chain.** A real crash
  surfaced through the position-value chart (story 10), which calls this provider for many
  historical dates per chart build: some dates return a response with `meta` but no
  `indicators`/`quote`/`close` at all (e.g. a date before the symbol's first trade), which
  the direct `GetProperty` chain turned into an unhandled `KeyNotFoundException` instead of
  the intended `PriceStatus.UnsupportedSecurity`. This was a pre-existing bug in the
  provider (not something the chart feature introduced), just never exercised by a caller
  that queries this many distinct historical dates before. Same fix applied to
  `meta`/`currency`, the other direct `GetProperty` chain in this method, for consistency.
  Covered by two regression tests using a fake `HttpMessageHandler` (`FixedResponseHandler`)
  instead of the real endpoint, since this response shape isn't reliably reproducible against
  live data.
- **`PositionValueChartService`'s per-sample-date price/FX lookups catch and log any
  unexpected exception from the underlying provider, converting it into a failed-lookup
  result (a gap for that one date) instead of letting it propagate and abort the whole chart
  build.** This is defense-in-depth on top of the Yahoo fix above — any *future* malformed
  response shape (from Yahoo or a later provider) degrades one chart point instead of the
  entire chart, consistent with AC3's "no crash on missing data points" applying to
  unexpected exceptions, not just the documented `PriceStatus`/`FxRateStatus` failure
  outcomes. `BuildChartAsync` also logs one `LogWarning` per (security, date) or
  (currency pair, date) gap actually excluded from a series (deduped across the initial-
  amount/comparison-shares lookups and the main sampling loop, which can both query the
  start date), plus a summary `LogInformation` line with resolved-point counts for both
  series — all per an explicit user request to make chart data gaps and failures
  diagnosable from the Logs page rather than only visible as "fewer points than expected"
  or a generic caught-exception message.
- **A custom `LoggingErrorBoundary` (`ErrorBoundary` subclass overriding `OnErrorAsync`) wraps
  every routed page in `Routes.razor`, keyed per navigation (`@key="routeData"`).** Per an
  explicit user report: navigating to a page that threw during render (observed on the
  position-value chart page) showed .NET MAUI `BlazorWebView`'s generic "An unhandled error
  has occurred / Reload" banner (`index.html`'s `#blazor-error-ui`), with no way to see what
  actually went wrong and no path back except a full app reload. The boundary logs the real
  exception via the normal `ILogger` pipeline (so it's on the Logs page) before rendering a
  friendly in-page message with a link there, instead of the exception reaching
  `BlazorWebView`'s default handling at all. `@key="routeData"` forces a fresh boundary
  instance on every navigation — without it, `ErrorBoundary`'s "stay in ErrorContent until
  `Recover()` is called" behavior would keep showing the error page even after navigating to
  an unrelated route, since the Router's diffing would otherwise reuse the same boundary
  instance. `AppDomain.UnhandledException`/`TaskScheduler.UnobservedTaskException` handlers
  were also added in `MauiProgram.cs` as a belt-and-suspenders catch-all for exceptions
  outside any component's render/lifecycle (e.g. an un-awaited background `Task`), logged the
  same way — the error boundary alone only covers exceptions during rendering.
- **A new `LogActivityTracker` singleton (constructed once in `MauiProgram.CreateMauiApp`,
  shared with `FileLoggerProvider` and registered in DI for the Gui) flags whether a
  Warning-or-above line has been written since the Logs page was last opened, so
  `NavMenu.razor` can show a "New" badge on the Logs link.** Per an explicit user request
  that a new problem shouldn't sit silently in a file nobody's looking at. Deliberately not a
  full unread-count or per-entry read state — a single boolean flag, cleared the moment the
  Logs page loads its content (`OnInitialized` calls `MarkSeen()`), is enough to answer "is
  there something new to look at," which is all that was asked for. The tracker's `Changed`
  event can fire from any thread (logging can happen from a background task), so `NavMenu`
  marshals the resulting `StateHasChanged()` through `InvokeAsync`.
- **`VocabularyOverrideInflationRateProvider` looks up its override by a case-insensitive
  key match (fetching all `InflationRateOverride` entries and comparing client-side) instead
  of `IVocabularyRepository.GetValueAsync`'s exact-match lookup, and tolerates a few
  hand-typed value formats (surrounding whitespace, a trailing "%", a comma decimal
  separator) instead of requiring the exact documented "5.2" shape.** Per a real user report:
  after adding an override for `USD:2026`, the chart still logged "no rate available" with
  no indication of why — with the original exact-match lookup, a key or value that didn't
  match byte-for-byte (e.g. typed as "usd:2026", or with a "%" sign) would silently behave
  identically to no override existing at all. The provider now also logs the outcome via a
  newly-added `ILogger` dependency: which key it looked for, whether an entry was found, and
  — if found but unparseable — the exact value that failed and why, at `Warning` (visible on
  the Logs page) rather than silently falling through. This is the first Core/Data-layer
  class in this app to take an `ILogger` dependency (`Microsoft.Extensions.Logging.Abstractions`
  added to `PortfolioCalc.Core.csproj`); every other provider in this project just returns a
  `Result` with an `ErrorMessage` and lets the caller decide whether to log, but this class
  exists specifically as an operator-facing "did my manual override actually apply" tool, so
  logging its own reasoning here (rather than only in the App-layer caller, which has no
  visibility into *why* the override step failed) is warranted.
  <br>The lenient value parsing deliberately excludes `NumberStyles.AllowThousands`: an
  earlier version of this fix used the default `NumberStyles.Number`, under which "5,2"
  parsed successfully as `52` (comma read as a thousands separator) instead of falling
  through to the comma-as-decimal-point fallback — silently 10x-wrong is worse than the
  original "not parseable" failure it was meant to fix, and was caught by a test
  (`GetRateAsync_tolerates_common_hand_typed_value_formats`) before shipping.
- **A position whose price or FX conversion doesn't come back `Success` is excluded from
  the portfolio value report's grand total and visually flagged, rather than shown with a
  caveat alongside a number.** `SecurityPriceService`/`FxRateService.GetPriceAsync`/
  `GetRateAsync` already refuse to return a stored pending/rejected value (story 07) and
  fetch live instead — so by the time `PositionValuationService` sees a non-`Success`
  status, that's a real, current failure (unsupported security, network error, or the live
  refetch itself came back pending/unusable), not a stale flag to second-guess; there's
  nothing left to reuse into an inline caveat, only a hole to leave out of the sum.
