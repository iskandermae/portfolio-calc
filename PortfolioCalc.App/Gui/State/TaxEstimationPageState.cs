using PortfolioCalc.App.Application.Tax;

namespace PortfolioCalc.App.Gui.State;

/// <summary>Holds the Tax Estimation page's form inputs and last result across navigations —
/// registered Scoped (one instance per app session in this MAUI Blazor Hybrid app), unlike
/// the page component itself, which is torn down and recreated every time the user navigates
/// away and back. Per an explicit user request that leaving the tab and returning shouldn't
/// lose what was entered.</summary>
public sealed class TaxEstimationPageState
{
    public sealed class ProposedRow
    {
        public int PositionId { get; set; }
        public decimal? Shares { get; set; }
        public decimal? Amount { get; set; }
    }

    public DateOnly? TaxYearStart { get; set; }
    public int? AccountIdFilter { get; set; }
    public List<ProposedRow> ProposedRows { get; set; } = [new()];
    public TaxEstimationReport? Report { get; set; }
    public string? Error { get; set; }
}
