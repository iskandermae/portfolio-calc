namespace PortfolioCalc.Core.Domain;

/// <summary>A saved column layout/sort blob for one GUI screen — see
/// doc/stories/08-transactions-report.md. One row per screen, keyed by an
/// app-defined identifier (e.g. "TransactionsReport"); the JSON shape is owned by
/// that screen, not interpreted here.</summary>
public class UiLayoutSetting
{
    public required string ScreenKey { get; set; }
    public required string LayoutJson { get; set; }
}
