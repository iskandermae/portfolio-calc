using System.Globalization;
using Microsoft.Extensions.Logging;
using PortfolioCalc.Core.Domain;
using PortfolioCalc.Core.Inflation;
using PortfolioCalc.Core.Repositories;

namespace PortfolioCalc.Core.Data.Inflation;

/// <summary>Wraps another <see cref="IInflationRateProvider"/> (currently
/// <see cref="WorldBankInflationRateProvider"/>) with a manual-override fallback: when the
/// wrapped provider can't resolve a rate — most commonly because the current/very recent
/// year's CPI figure isn't published yet — an "InflationRateOverride"
/// <see cref="VocabularyEntry"/> row, keyed "{baseCurrency}:{year}" (e.g. "USD:2026") with a
/// percentage-number value (matching <see cref="InflationRate.Rate"/>'s convention), lets the
/// user fill the gap by hand from the Vocabularies page instead of waiting on the real data
/// source. Only consulted when the wrapped provider fails — an override never shadows a real,
/// published rate. See doc/decisions.md.</summary>
public sealed class VocabularyOverrideInflationRateProvider(
    IInflationRateProvider innerProvider,
    IVocabularyRepository vocabularyRepository,
    ILogger<VocabularyOverrideInflationRateProvider> logger) : IInflationRateProvider
{
    public async Task<InflationRateResult> GetRateAsync(
        string baseCurrency, DateOnly period, CancellationToken cancellationToken = default)
    {
        var result = await innerProvider.GetRateAsync(baseCurrency, period, cancellationToken);
        if (result.Status == InflationRateStatus.Success)
            return result;

        var key = BuildKey(baseCurrency, period.Year);

        // Looked up via GetByTypeAsync + a case-insensitive match here, rather than
        // IVocabularyRepository.GetValueAsync's exact-match lookup — a user typing the key
        // by hand (e.g. "usd:2026") is an easy, otherwise-silent way to miss the override
        // entirely; the dataset for one vocabulary type is small enough that this is cheap.
        var entries = await vocabularyRepository.GetByTypeAsync(VocabularyTypes.InflationRateOverride);
        var entry = entries.FirstOrDefault(e => string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase));

        if (entry is null)
        {
            logger.LogInformation(
                "No InflationRateOverride entry found for key \"{Key}\" — the real inflation source failed " +
                "with: {Reason}",
                key, result.ErrorMessage);
            return result;
        }

        if (TryParsePercentage(entry.Value, out var rate))
        {
            logger.LogInformation("Using InflationRateOverride entry \"{Key}\" = \"{Value}\" ({Rate}%).", key, entry.Value, rate);
            return InflationRateResult.Ok(rate);
        }

        logger.LogWarning(
            "InflationRateOverride entry \"{Key}\" has value \"{Value}\", which could not be parsed as a " +
            "percentage number (e.g. \"5.2\" for 5.2%) — ignoring it and falling back to the original failure: " +
            "{Reason}. Fix the value on the Vocabularies page.",
            key, entry.Value, result.ErrorMessage);
        return result;
    }

    /// <summary>Deliberately excludes <see cref="NumberStyles.AllowThousands"/> — a
    /// thousands-grouping comma is never a real use case for a percentage rate, and allowing
    /// it would make "5,2" (a European-locale decimal 5.2) silently parse as 52 instead
    /// (comma treated as a group separator) rather than being caught by the comma-as-decimal
    /// fallback below.</summary>
    private const NumberStyles PercentageNumberStyles =
        NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign;

    /// <summary>Tolerates the formats a user is likely to type by hand: surrounding
    /// whitespace, a trailing "%", and a comma as the decimal separator (e.g. "5,2" from a
    /// European locale) in addition to the documented plain-dot format (e.g. "5.2").</summary>
    private static bool TryParsePercentage(string rawValue, out decimal rate)
    {
        var trimmed = rawValue.Trim().TrimEnd('%').Trim();
        if (decimal.TryParse(trimmed, PercentageNumberStyles, CultureInfo.InvariantCulture, out rate))
            return true;

        return decimal.TryParse(trimmed.Replace(',', '.'), PercentageNumberStyles, CultureInfo.InvariantCulture, out rate);
    }

    public static string BuildKey(string baseCurrency, int year) => $"{baseCurrency.ToUpperInvariant()}:{year}";
}
