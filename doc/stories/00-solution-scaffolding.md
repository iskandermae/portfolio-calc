# Story: Solution Scaffolding

## Description
Set up the initial .NET solution structure so all subsequent stories have a project to
build on: solution file, the two combined projects (Core, App), the test project, and
baseline EF Core + SQLite wiring with an empty schema.

## Acceptance Criteria
- [ ] `PortfolioCalc.sln` created with `PortfolioCalc.Core`, `PortfolioCalc.App` (.NET MAUI
      Blazor Hybrid), and `PortfolioCalc.Core.Tests` (xUnit) projects, targeting .NET 8.
- [ ] `PortfolioCalc.Core` has `Core/` and `Data/` folders per the layering rules in
      CLAUDE.md.
- [ ] `PortfolioCalc.App` has `Application/` and `Gui/` folders.
- [ ] EF Core + SQLite provider referenced in `PortfolioCalc.Core`; a `DbContext` exists
      with no tables yet (or a placeholder table), and a local `.sqlite` file is created on
      first run in a per-user app data folder (not the repo).
- [ ] `dotnet build`, `dotnet test`, and `dotnet run --project PortfolioCalc.App` all
      succeed with an empty/placeholder UI shell.
- [ ] `.gitignore` excludes build output and the local SQLite data file.

## Technical Notes
- This story has no functional behavior — it exists purely to unblock every other story.
- Confirm the MAUI Blazor Hybrid workload is installed and the app launches as a desktop
  window before marking this done.

## Dependencies / Open Questions
- None. This is the first story.
