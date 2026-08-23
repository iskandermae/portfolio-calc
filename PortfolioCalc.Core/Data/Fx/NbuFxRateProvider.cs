using System.Net.Http.Json;
using System.Text.Json;
using PortfolioCalc.Core.Fx;

namespace PortfolioCalc.Core.Data.Fx;

/// <summary>Fetches UAH exchange rates from the National Bank of Ukraine's public statistics
/// API — Frankfurter doesn't cover UAH (see doc/stories/12-tax-estimation-report.md). Only
/// handles pairs where one side is UAH; the composite <see cref="CompositeFxRateProvider"/>
/// routes everything else to <see cref="FrankfurterFxRateProvider"/>.</summary>
public sealed class NbuFxRateProvider : IFxRateProvider
{
    private const string DefaultBaseUrl = "https://bank.gov.ua/NBUStatService/v1/statdirectory/exchange";
    private const string UahCode = "UAH";

    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    /// <param name="baseUrl">Overridable for tests that need to point at an unreachable
    /// endpoint to exercise the network-failure path; production callers should omit it.</param>
    public NbuFxRateProvider(HttpClient httpClient, string baseUrl = DefaultBaseUrl)
    {
        _httpClient = httpClient;
        _baseUrl = baseUrl;
    }

    public async Task<FxRateResult> GetRateAsync(
        string fromCurrency, string toCurrency, DateOnly date, CancellationToken cancellationToken = default)
    {
        if (string.Equals(fromCurrency, toCurrency, StringComparison.OrdinalIgnoreCase))
            return FxRateResult.Ok(1m);

        var fromIsUah = string.Equals(fromCurrency, UahCode, StringComparison.OrdinalIgnoreCase);
        var toIsUah = string.Equals(toCurrency, UahCode, StringComparison.OrdinalIgnoreCase);
        if (!fromIsUah && !toIsUah)
            return FxRateResult.Unsupported(
                $"{fromCurrency}/{toCurrency} doesn't involve UAH — not handled by the NBU provider.");

        // The NBU API reports "how many UAH for 1 unit of the other currency", so a
        // UAH -> other request inverts that rate.
        var otherCurrency = fromIsUah ? toCurrency : fromCurrency;
        var uahPerUnit = await FetchUahPerUnitAsync(otherCurrency, date, cancellationToken);
        if (uahPerUnit.Status != FxRateStatus.Success)
            return uahPerUnit;

        return FxRateResult.Ok(fromIsUah ? 1m / uahPerUnit.Rate!.Value : uahPerUnit.Rate!.Value);
    }

    private async Task<FxRateResult> FetchUahPerUnitAsync(
        string currencyCode, DateOnly date, CancellationToken cancellationToken)
    {
        var url = $"{_baseUrl}?valcode={Uri.EscapeDataString(currencyCode)}&date={date:yyyyMMdd}&json";

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(url, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            return FxRateResult.NetworkFailure(
                $"Network error fetching NBU rate for {currencyCode} on {date:yyyy-MM-dd}: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return FxRateResult.NetworkFailure(
                $"Timed out fetching NBU rate for {currencyCode} on {date:yyyy-MM-dd}: {ex.Message}");
        }

        if (!response.IsSuccessStatusCode)
            return FxRateResult.NetworkFailure(
                $"NBU rate request for {currencyCode} failed with status {(int)response.StatusCode}");

        List<NbuQuote>? quotes;
        try
        {
            quotes = await response.Content.ReadFromJsonAsync<List<NbuQuote>>(cancellationToken);
        }
        catch (JsonException ex)
        {
            return FxRateResult.NetworkFailure($"Could not parse NBU response for {currencyCode}: {ex.Message}");
        }

        var quote = quotes?.FirstOrDefault();
        if (quote is null)
            return FxRateResult.Unsupported($"No NBU rate returned for {currencyCode} on {date:yyyy-MM-dd}");

        return FxRateResult.Ok(quote.Rate);
    }

    private sealed record NbuQuote(decimal Rate);
}
