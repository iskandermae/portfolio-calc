namespace PortfolioCalc.Core.Import.Ibkr;

/// <summary>One row from a broker export, kept as raw attribute strings with no
/// interpretation — the parser's job is only to turn a file into rows; deciding what a
/// row means (mapping, aggregation, skip/unrecognized classification) is
/// Application-layer logic (see doc/decisions.md).</summary>
public sealed record IbkrRawRow(string ElementName, IReadOnlyDictionary<string, string> Attributes)
{
    public string? Get(string attributeName) => Attributes.GetValueOrDefault(attributeName);
}
