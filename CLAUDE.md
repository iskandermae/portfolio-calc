# CLAUDE.md

Backlog: [doc/stories/](doc/stories/).

## Working style
- Flag ambiguities/discrepancies between requirements and code before implementing — don't guess silently.
- If you notice an existing bug or smell while touching a file, mention it; fix only if trivial and in-scope, otherwise report it instead of scope-creeping.
- Prefer refactoring over duplicating when a change touches an existing pattern (DRY), but don't refactor unrelated code you didn't  to touch - report this.
- After executing a task, briefly report (if relevant):
  - assumptions or limitations;
  - existing issues discovered but not fixed in a form of new task.

## Non-obvious domain rules

- Base currency change → reports recompute on the fly from stored FX history, no migration.
- Synthetic growth rate: solve `currentPrice = initPrice * (1 + r)^n` on a days-elapsed basis, annualize.

## Architecture

```
PortfolioCalc.sln
├── PortfolioCalc.Core/     Core/ (domain, interfaces, calc logic) + Data/ (EF Core/SQLite, file parser, FX/price/inflation fetchers)
├── PortfolioCalc.App/      Application/ (use-case services) + Gui/ (MAUI Blazor Hybrid, Razor, charts)
└── PortfolioCalc.Core.Tests/
```

- `Core` depends only on its own interfaces, never on `Data` concretes.
- `Gui` may call `Core` repository interfaces directly for simple CRUD; anything with actual logic (valuation, growth rate, quality validation) goes through `Application`.
- New external source (price/FX/inflation/broker format) = new `Data` class behind an existing `Core` interface. Never hard-code providers into app logic.

## Stack

.NET 10, EF Core + SQLite, .NET MAUI Blazor Hybrid, xUnit.

## Build & Test

```
dotnet build
dotnet test
dotnet run --project PortfolioCalc.App
```

## Stories

- `doc/stories/NN-*.md`
- Update the story file if scope changes; don't let it drift.
