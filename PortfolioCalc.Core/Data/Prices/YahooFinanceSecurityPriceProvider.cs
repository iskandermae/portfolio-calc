using System.Net.Http.Headers;
using System.Text.Json;
using PortfolioCalc.Core.Prices;

namespace PortfolioCalc.Core.Data.Prices;

/// <summary>Fetches security prices from Yahoo Finance's public (undocumented, no-API-key)
/// "chart" endpoint — the same one <c>yfinance</c> (github.com/ranaroussi/yfinance) wraps.
/// Passes this app's <c>Security.Symbol</c> straight through as the Yahoo ticker: this works
/// unmodified for plain US-listed tickers (the common case for this app's IBKR imports), but
/// non-US listings that need a Yahoo exchange suffix (e.g. "VOD.L") aren't mapped — those,
/// and any symbol Yahoo doesn't recognize, come back as <see cref="PriceStatus.UnsupportedSecurity"/>
/// per call rather than being worked around up front. See doc/decisions.md.
/// <para>One exchange quirk is handled rather than left as a gap: Yahoo quotes London-listed
/// securities in pence ("GBp"), not pounds — a request for the security's actual currency,
/// GBP (one of <see cref="PortfolioCalc.Core.Domain.SupportedCurrencies.Codes"/>, and a pair
/// <c>FrankfurterFxRateProvider</c> already covers), is served by converting the pence price
/// on the fly (÷100) rather than being rejected as a currency mismatch. See
/// doc/decisions.md.</para></summary>
public sealed class YahooFinanceSecurityPriceProvider : ISecurityPriceProvider
{
    private const string DefaultBaseUrl = "https://query1.finance.yahoo.com/v8/finance/chart";

    /// <summary>Yahoo's currency code for UK securities quoted in pence rather than pounds.</summary>
    private const string PenceCurrencyCode = "GBp";
    private const decimal PenceToPounds = 100m;

    // Yahoo's edge rejects requests with no User-Agent header (observed: HTTP 429 "Edge:
    // Too Many Requests" even for a first, unthrottled request) — this isn't a rate limit,
    // it's a bot filter, so any browser-like value satisfies it.
    private static readonly ProductInfoHeaderValue UserAgentHeader = new("Mozilla", "5.0");

    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    /// <param name="baseUrl">Overridable for tests that need to point at an unreachable
    /// endpoint to exercise the network-failure path; production callers should omit it.</param>
    public YahooFinanceSecurityPriceProvider(HttpClient httpClient, string baseUrl = DefaultBaseUrl)
    {
        _httpClient = httpClient;
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Contains(UserAgentHeader))
            _httpClient.DefaultRequestHeaders.UserAgent.Add(UserAgentHeader);
        _baseUrl = baseUrl;
    }

    public async Task<PriceResult> GetPriceAsync(
        string symbol,
        string currency,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        // A single UTC calendar day window: Yahoo's chart endpoint has no "give me exactly
        // this date" query, only a period1/period2 timestamp range, so the window is
        // narrowed to one day and whatever single close (if any) falls in it is used.
        var period1 = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeSeconds();
        var period2 = new DateTimeOffset(date.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeSeconds();
        var url = $"{_baseUrl}/{Uri.EscapeDataString(symbol)}?interval=1d&period1={period1}&period2={period2}";

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(url, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            return PriceResult.NetworkFailure(
                $"Network error fetching price for {symbol} on {date:yyyy-MM-dd}: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return PriceResult.NetworkFailure(
                $"Timed out fetching price for {symbol} on {date:yyyy-MM-dd}: {ex.Message}");
        }

        // Yahoo returns a structured JSON error body (chart.error) with a 404 status for an
        // unrecognized symbol, not just a bare non-2xx — so the body is always parsed first,
        // regardless of status code, and only an unparsable body is treated as a network error.
        JsonDocument document;
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            document = JsonDocument.Parse(body);
        }
        catch (JsonException ex)
        {
            return PriceResult.NetworkFailure(
                $"Could not parse price response for {symbol}: {ex.Message}");
        }

        using (document)
        {
            var chart = document.RootElement.GetProperty("chart");

            if (chart.TryGetProperty("error", out var errorElement) && errorElement.ValueKind != JsonValueKind.Null)
            {
                var description = errorElement.TryGetProperty("description", out var descriptionElement)
                    ? descriptionElement.GetString()
                    : null;
                return PriceResult.Unsupported(
                    $"Yahoo Finance has no data for symbol {symbol}: {description ?? "unknown symbol"}");
            }

            if (!response.IsSuccessStatusCode)
                return PriceResult.NetworkFailure(
                    $"Price request for {symbol} failed with status {(int)response.StatusCode}");

            if (!chart.TryGetProperty("result", out var resultElement) ||
                resultElement.ValueKind != JsonValueKind.Array ||
                resultElement.GetArrayLength() == 0)
            {
                return PriceResult.Unsupported($"No price data returned for {symbol} on {date:yyyy-MM-dd}");
            }

            var result = resultElement[0];
            var meta = result.GetProperty("meta");
            var resultCurrency = meta.GetProperty("currency").GetString();

            // "GBp" (pence) is a distinct currency code from "GBP" (pounds) in Yahoo's data —
            // ordinal, case-sensitive comparison here so a plain GBP-quoted security isn't
            // mistaken for a pence-quoted one, or vice versa.
            var isPenceQuotedAsPounds =
                string.Equals(resultCurrency, PenceCurrencyCode, StringComparison.Ordinal) &&
                string.Equals(currency, "GBP", StringComparison.OrdinalIgnoreCase);

            if (!isPenceQuotedAsPounds && !string.Equals(resultCurrency, currency, StringComparison.OrdinalIgnoreCase))
            {
                return PriceResult.Unsupported(
                    $"Symbol {symbol} is quoted in {resultCurrency}, not the requested {currency}");
            }

            var closes = result
                .GetProperty("indicators")
                .GetProperty("quote")[0]
                .GetProperty("close");

            if (closes.ValueKind != JsonValueKind.Array || closes.GetArrayLength() == 0)
                return PriceResult.Unsupported($"No trading data for {symbol} on {date:yyyy-MM-dd} (market likely closed)");

            var close = closes[0];
            if (close.ValueKind != JsonValueKind.Number)
                return PriceResult.Unsupported($"No trading data for {symbol} on {date:yyyy-MM-dd} (market likely closed)");

            var price = close.GetDecimal();
            if (isPenceQuotedAsPounds)
                price /= PenceToPounds;

            return PriceResult.Ok(price);
        }
    }
}
