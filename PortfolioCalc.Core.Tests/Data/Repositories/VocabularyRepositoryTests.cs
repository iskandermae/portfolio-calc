using Microsoft.EntityFrameworkCore;
using PortfolioCalc.Core.Data;
using PortfolioCalc.Core.Data.Repositories;
using PortfolioCalc.Core.Domain;

namespace PortfolioCalc.Core.Tests.Data.Repositories;

public class VocabularyRepositoryTests
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

    [Fact]
    public async Task EnsureCreated_seeds_the_ExchangeYahooSuffix_vocabulary()
    {
        using var context = CreateOpenInMemoryContext();
        var repository = new VocabularyRepository(context);

        var entries = await repository.GetByTypeAsync(VocabularyTypes.ExchangeYahooSuffix);

        Assert.Contains(entries, e => e.Key == "LSEETF" && e.Value == ".L");
        Assert.Contains(entries, e => e.Key == "IBIS" && e.Value == ".DE");
        Assert.Contains(entries, e => e.Key == "ARCA" && e.Value == "");
    }

    [Fact]
    public async Task GetValueAsync_returns_null_for_an_unmapped_key()
    {
        using var context = CreateOpenInMemoryContext();
        var repository = new VocabularyRepository(context);

        var value = await repository.GetValueAsync(VocabularyTypes.ExchangeYahooSuffix, "SOMEEXOTICMARKET");

        Assert.Null(value);
    }

    [Fact]
    public async Task GetTypesAsync_returns_the_distinct_vocabulary_types()
    {
        using var context = CreateOpenInMemoryContext();
        var repository = new VocabularyRepository(context);

        var types = await repository.GetTypesAsync();

        Assert.Contains(VocabularyTypes.ExchangeYahooSuffix, types);
    }

    [Fact]
    public async Task AddAsync_then_GetValueAsync_round_trips_a_new_entry()
    {
        using var context = CreateOpenInMemoryContext();
        var repository = new VocabularyRepository(context);

        await repository.AddAsync(new VocabularyEntry
        {
            VocabularyType = "SomeOtherVocabulary", Key = "X", Value = "Y", Description = "test",
        });

        Assert.Equal("Y", await repository.GetValueAsync("SomeOtherVocabulary", "X"));
    }

    [Fact]
    public async Task UpdateAsync_changes_the_value_and_description_but_not_the_key()
    {
        using var context = CreateOpenInMemoryContext();
        var repository = new VocabularyRepository(context);
        var entry = await repository.AddAsync(new VocabularyEntry
        {
            VocabularyType = "SomeOtherVocabulary", Key = "X", Value = "Y",
        });

        entry.Value = "Z";
        entry.Description = "updated";
        await repository.UpdateAsync(entry);

        var reloaded = (await repository.GetByTypeAsync("SomeOtherVocabulary")).Single();
        Assert.Equal("X", reloaded.Key);
        Assert.Equal("Z", reloaded.Value);
        Assert.Equal("updated", reloaded.Description);
    }

    [Fact]
    public async Task DeleteAsync_removes_the_entry()
    {
        using var context = CreateOpenInMemoryContext();
        var repository = new VocabularyRepository(context);
        var entry = await repository.AddAsync(new VocabularyEntry
        {
            VocabularyType = "SomeOtherVocabulary", Key = "X", Value = "Y",
        });

        await repository.DeleteAsync(entry.Id);

        Assert.Empty(await repository.GetByTypeAsync("SomeOtherVocabulary"));
    }
}
