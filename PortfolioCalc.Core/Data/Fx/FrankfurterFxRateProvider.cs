using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PortfolioCalc.Core.Fx;

namespace PortfolioCalc.Core.Data.Fx;

/// <summary>Fetches FX rates from the Frankfurter API (frankfurter.dev), a free, no-API-key
/// service backed by ECB reference rates. Covers major currencies (EUR, USD, GBP, JPY, ...)
/// but not every currency a broker export might report — an unsupported pair is a per-call
/// <see cref="FxRateStatus.UnsupportedCurrency"/>, not a reason to add another provider
/// before one is actually needed (see doc/stories/03-fetch-cross-currency-rates.md).</summary>
public sealed class FrankfurterFxRateProvider : IFxRateProvider
{
    private const string DefaultBaseUrl = "https://api.frankfurter.dev/v2/rates";

    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    /// <param name="baseUrl">Overridable for tests that need to point at an unreachable
    /// endpoint to exercise the network-failure path; production callers should omit it.</param>
    public FrankfurterFxRateProvider(HttpClient httpClient, string baseUrl = DefaultBaseUrl)
    {
        _httpClient = httpClient;
        _baseUrl = baseUrl;
    }

    public async Task<FxRateResult> GetRateAsync(
        string fromCurrency,
        string toCurrency,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(fromCurrency, toCurrency, StringComparison.OrdinalIgnoreCase))
            return FxRateResult.Ok(1m);

        var url = $"{_baseUrl}?date={date:yyyy-MM-dd}" +
                   $"&base={Uri.EscapeDataString(fromCurrency)}" +
                   $"&quotes={Uri.EscapeDataString(toCurrency)}";

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(url, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            return FxRateResult.NetworkFailure(
                $"Network error fetching {fromCurrency}/{toCurrency} rate for {date:yyyy-MM-dd}: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return FxRateResult.NetworkFailure(
                $"Timed out fetching {fromCurrency}/{toCurrency} rate for {date:yyyy-MM-dd}: {ex.Message}");
        }

        if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
            return FxRateResult.Unsupported($"Unsupported currency pair {fromCurrency}/{toCurrency}");

        if (!response.IsSuccessStatusCode)
            return FxRateResult.NetworkFailure(
                $"FX rate request for {fromCurrency}/{toCurrency} failed with status {(int)response.StatusCode}");

        List<FrankfurterQuote>? quotes;
        try
        {
            quotes = await response.Content.ReadFromJsonAsync<List<FrankfurterQuote>>(cancellationToken);
        }
        catch (JsonException ex)
        {
            return FxRateResult.NetworkFailure(
                $"Could not parse FX rate response for {fromCurrency}/{toCurrency}: {ex.Message}");
        }

        var quote = quotes?.FirstOrDefault();
        if (quote is null)
            return FxRateResult.Unsupported(
                $"No rate returned for {fromCurrency}/{toCurrency} on {date:yyyy-MM-dd}");

        return FxRateResult.Ok(quote.Rate);
    }

    private sealed record FrankfurterQuote(string Date, string Base, string Quote, decimal Rate);
}
