using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PortfolioCalc.Core.Data;
using PortfolioCalc.Core.Data.Inflation;
using PortfolioCalc.Core.Data.Repositories;
using PortfolioCalc.Core.Domain;
using PortfolioCalc.Core.Inflation;
using PortfolioCalc.Core.Repositories;

namespace PortfolioCalc.Core.Tests.Data.Inflation;

public class VocabularyOverrideInflationRateProviderTests
{
    private static PortfolioDbContext CreateOpenInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<PortfolioDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var context = new PortfolioDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }

    private static VocabularyOverrideInflationRateProvider CreateProvider(
        IInflationRateProvider inner, IVocabularyRepository vocabularyRepository) =>
        new(inner, vocabularyRepository, NullLogger<VocabularyOverrideInflationRateProvider>.Instance);

    private sealed class FakeInflationRateProvider(InflationRateResult result) : IInflationRateProvider
    {
        public int CallCount { get; private set; }

        public Task<InflationRateResult> GetRateAsync(
            string baseCurrency, DateOnly period, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(result);
        }
    }

    [Fact]
    public async Task GetRateAsync_returns_the_inner_providers_result_when_it_succeeds()
    {
        using var context = CreateOpenInMemoryContext();
        var inner = new FakeInflationRateProvider(InflationRateResult.Ok(4.7m));
        var provider = CreateProvider(inner, new VocabularyRepository(context));

        var result = await provider.GetRateAsync("USD", new DateOnly(2021, 1, 1));

        Assert.Equal(InflationRateStatus.Success, result.Status);
        Assert.Equal(4.7m, result.Rate);
    }

    [Fact]
    public async Task GetRateAsync_falls_back_to_a_matching_vocabulary_override_when_the_inner_provider_fails()
    {
        using var context = CreateOpenInMemoryContext();
        var repository = new VocabularyRepository(context);
        await repository.AddAsync(new VocabularyEntry
        {
            VocabularyType = VocabularyTypes.InflationRateOverride,
            Key = "USD:2026",
            Value = "5.2",
        });

        var inner = new FakeInflationRateProvider(InflationRateResult.Unsupported("not published yet"));
        var provider = CreateProvider(inner, repository);

        var result = await provider.GetRateAsync("usd", new DateOnly(2026, 1, 1));

        Assert.Equal(InflationRateStatus.Success, result.Status);
        Assert.Equal(5.2m, result.Rate);
    }

    [Fact]
    public async Task GetRateAsync_returns_the_original_failure_when_no_override_is_configured()
    {
        using var context = CreateOpenInMemoryContext();
        var inner = new FakeInflationRateProvider(InflationRateResult.Unsupported("not published yet"));
        var provider = CreateProvider(inner, new VocabularyRepository(context));

        var result = await provider.GetRateAsync("USD", new DateOnly(2026, 1, 1));

        Assert.Equal(InflationRateStatus.UnsupportedCurrency, result.Status);
        Assert.Null(result.Rate);
    }

    [Fact]
    public async Task GetRateAsync_ignores_an_override_whose_value_is_not_a_number()
    {
        using var context = CreateOpenInMemoryContext();
        var repository = new VocabularyRepository(context);
        await repository.AddAsync(new VocabularyEntry
        {
            VocabularyType = VocabularyTypes.InflationRateOverride,
            Key = "USD:2026",
            Value = "not-a-number",
        });

        var inner = new FakeInflationRateProvider(InflationRateResult.Unsupported("not published yet"));
        var provider = CreateProvider(inner, repository);

        var result = await provider.GetRateAsync("USD", new DateOnly(2026, 1, 1));

        Assert.Equal(InflationRateStatus.UnsupportedCurrency, result.Status);
    }

    /// <summary>Regression test for a real report: the user added an override under the key
    /// as typed on the Vocabularies page, but the lookup used to be an exact (case-sensitive)
    /// match — a key entered as "usd:2026" would silently never match "USD:2026" and the
    /// original failure would keep showing up as if no override existed at all.</summary>
    [Fact]
    public async Task GetRateAsync_matches_a_vocabulary_key_regardless_of_case()
    {
        using var context = CreateOpenInMemoryContext();
        var repository = new VocabularyRepository(context);
        await repository.AddAsync(new VocabularyEntry
        {
            VocabularyType = VocabularyTypes.InflationRateOverride,
            Key = "usd:2026",
            Value = "5.2",
        });

        var inner = new FakeInflationRateProvider(InflationRateResult.Unsupported("not published yet"));
        var provider = CreateProvider(inner, repository);

        var result = await provider.GetRateAsync("USD", new DateOnly(2026, 1, 1));

        Assert.Equal(InflationRateStatus.Success, result.Status);
        Assert.Equal(5.2m, result.Rate);
    }

    [Theory]
    [InlineData("5.2")]
    [InlineData(" 5.2 ")]
    [InlineData("5.2%")]
    [InlineData("5,2")]
    public async Task GetRateAsync_tolerates_common_hand_typed_value_formats(string typedValue)
    {
        using var context = CreateOpenInMemoryContext();
        var repository = new VocabularyRepository(context);
        await repository.AddAsync(new VocabularyEntry
        {
            VocabularyType = VocabularyTypes.InflationRateOverride,
            Key = "USD:2026",
            Value = typedValue,
        });

        var inner = new FakeInflationRateProvider(InflationRateResult.Unsupported("not published yet"));
        var provider = CreateProvider(inner, repository);

        var result = await provider.GetRateAsync("USD", new DateOnly(2026, 1, 1));

        Assert.Equal(InflationRateStatus.Success, result.Status);
        Assert.Equal(5.2m, result.Rate);
    }
}
