namespace PortfolioCalc.Core.Import.T212;

/// <summary>Parses a Trading 212 CSV export into raw rows. Implementation lives in Data/.
/// Mapping the raw rows onto the domain model (account/security resolution, dedup) is
/// Application-layer logic. Async because the GUI hands this a browser/WebView file stream
/// that only supports asynchronous reads (see doc/decisions.md) — never fall back to a
/// synchronous parse.</summary>
public interface IT212TransactionImporter
{
    Task<T212ImportData> ParseAsync(Stream csvStream, CancellationToken cancellationToken = default);
}
