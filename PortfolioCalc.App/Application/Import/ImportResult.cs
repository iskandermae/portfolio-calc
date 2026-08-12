namespace PortfolioCalc.App.Application.Import;

/// <summary>A raw import-source row that was not turned into a domain transaction, with
/// the reason why.</summary>
public sealed record SkippedRow(string ElementName, string Reason, IReadOnlyDictionary<string, string> RawData);

/// <summary>Result of running an import: what got imported, what was recognized as
/// not-a-transaction and intentionally skipped, and what couldn't be interpreted at all.</summary>
public sealed class ImportResult
{
    /// <summary>Human-readable description of each transaction that was created.</summary>
    public List<string> Imported { get; } = [];

    /// <summary>Rows recognized as a known non-transaction shape (e.g. an IBKR FX
    /// currency-conversion "trade") and deliberately not imported.</summary>
    public List<SkippedRow> RecognizedButSkipped { get; } = [];

    /// <summary>Rows that don't match any mapping this importer knows about.</summary>
    public List<SkippedRow> Unrecognized { get; } = [];

    public int ImportedCount => Imported.Count;
}
