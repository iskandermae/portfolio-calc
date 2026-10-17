namespace PortfolioCalc.Core.Import.T212;

/// <summary>Raw, uninterpreted contents of one Trading 212 "Transaction history" CSV
/// export. Unlike IBKR's Flex Query XML, the file has no sections and carries no account
/// identity at all (see doc/decisions.md) — just one flat list of rows.</summary>
public sealed class T212ImportData
{
    public required IReadOnlyList<T212RawRow> Rows { get; init; }
}
