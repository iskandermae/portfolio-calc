namespace PortfolioCalc.Core.Import.Ibkr;

/// <summary>Raw, uninterpreted contents of one IBKR Flex Query XML export (one
/// FlexStatement — a file with more than one is not currently supported).
/// <see cref="OpenPositions"/> is intentionally not exposed: it's a point-in-time
/// snapshot, not a transaction log, and is never imported (see doc/decisions.md).</summary>
public sealed class IbkrFlexQueryImportData
{
    /// <summary>The FlexStatement's own accountId attribute — the fallback account
    /// identity when no row carries a non-blank acctAlias.</summary>
    public required string StatementAccountId { get; init; }
    public required IReadOnlyList<IbkrRawRow> Trades { get; init; }
    public required IReadOnlyList<IbkrRawRow> CashTransactions { get; init; }
    public required IReadOnlyList<IbkrRawRow> Transfers { get; init; }
    /// <summary>Rows from TransactionTaxes/TradeTransfers/CorporateActions — empty in
    /// today's sample, but if a future export has content there it must surface as
    /// unrecognized, not be silently dropped.</summary>
    public required IReadOnlyList<IbkrRawRow> UnhandledSectionRows { get; init; }
}
