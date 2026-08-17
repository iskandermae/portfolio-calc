using System.Net.Http.Json;
using System.Text.Json;
using PortfolioCalc.Core.Inflation;

namespace PortfolioCalc.Core.Data.Inflation;

/// <summary>Fetches annual CPI inflation from the World Bank API (api.worldbank.org), a
/// free, no-API-key service. The World Bank indexes data by country/region, not currency,
/// so each base currency maps to one representative country/region code (e.g. EUR to the
/// World Bank's "EMU" euro-area aggregate) — see doc/decisions.md. A currency with no
/// mapping, or a year the World Bank has no data for yet, is
/// <see cref="InflationRateStatus.UnsupportedCurrency"/>, mirroring
/// <see cref="PortfolioCalc.Core.Data.Fx.FrankfurterFxRateProvider"/>'s shape.</summary>
public sealed class WorldBankInflationRateProvider : IInflationRateProvider
{
    private const string DefaultBaseUrl = "https://api.worldbank.org/v2/country";
    private const string Indicator = "FP.CPI.TOTL.ZG";

    private static readonly IReadOnlyDictionary<string, string> CountryCodeByCurrency =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["USD"] = "US",
            ["EUR"] = "EMU",
            ["GBP"] = "GB",
            ["CHF"] = "CH",
            ["JPY"] = "JP",
            ["AUD"] = "AU",
            ["CAD"] = "CA",
        };

    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    /// <param name="baseUrl">Overridable for tests that need to point at an unreachable
    /// endpoint to exercise the network-failure path; production callers should omit it.</param>
    public WorldBankInflationRateProvider(HttpClient httpClient, string baseUrl = DefaultBaseUrl)
    {
        _httpClient = httpClient;
        _baseUrl = baseUrl;
    }

    public async Task<InflationRateResult> GetRateAsync(
        string baseCurrency,
        DateOnly period,
        CancellationToken cancellationToken = default)
    {
        if (!CountryCodeByCurrency.TryGetValue(baseCurrency, out var countryCode))
            return InflationRateResult.Unsupported($"No inflation data source mapped for currency {baseCurrency}");

        var year = period.Year;
        var url = $"{_baseUrl}/{countryCode}/indicator/{Indicator}?format=json&date={year}";

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(url, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            return InflationRateResult.NetworkFailure(
                $"Network error fetching {baseCurrency} inflation rate for {year}: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return InflationRateResult.NetworkFailure(
                $"Timed out fetching {baseCurrency} inflation rate for {year}: {ex.Message}");
        }

        if (!response.IsSuccessStatusCode)
            return InflationRateResult.NetworkFailure(
                $"Inflation rate request for {baseCurrency} failed with status {(int)response.StatusCode}");

        List<JsonElement>? envelope;
        try
        {
            envelope = await response.Content.ReadFromJsonAsync<List<JsonElement>>(cancellationToken);
        }
        catch (JsonException ex)
        {
            return InflationRateResult.NetworkFailure(
                $"Could not parse inflation rate response for {baseCurrency}: {ex.Message}");
        }

        // The World Bank envelope is [metadata, data[]]; data is null/absent when the
        // country/indicator/date combination has no data at all (e.g. an invalid country
        // code, or - since indicator responses only include a message element instead -
        // a request the API itself rejected).
        if (envelope is null || envelope.Count < 2 || envelope[1].ValueKind != JsonValueKind.Array)
            return InflationRateResult.Unsupported(
                $"No inflation data available for {baseCurrency} in {year}");

        var entry = envelope[1].EnumerateArray().FirstOrDefault();
        if (entry.ValueKind == JsonValueKind.Undefined ||
            !entry.TryGetProperty("value", out var valueProperty) ||
            valueProperty.ValueKind != JsonValueKind.Number)
        {
            return InflationRateResult.Unsupported(
                $"No inflation data available for {baseCurrency} in {year}");
        }

        return InflationRateResult.Ok(valueProperty.GetDecimal());
    }
}
