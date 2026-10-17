namespace PortfolioCalc.Core.Import.T212;

/// <summary>One row from a Trading 212 "Transaction history" CSV export, keyed by the
/// file's own header column names and kept with no interpretation — the parser's job is
/// only to turn a file into rows; deciding what a row means (mapping, aggregation,
/// skip/unrecognized classification) is Application-layer logic (see doc/decisions.md).</summary>
public sealed record T212RawRow(IReadOnlyDictionary<string, string> Fields)
{
    public string? Get(string columnName) => Fields.GetValueOrDefault(columnName);
}
