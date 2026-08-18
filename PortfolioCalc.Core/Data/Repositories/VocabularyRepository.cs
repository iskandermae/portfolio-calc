using Microsoft.EntityFrameworkCore;
using PortfolioCalc.Core.Domain;
using PortfolioCalc.Core.Repositories;

namespace PortfolioCalc.Core.Data.Repositories;

public class VocabularyRepository(PortfolioDbContext context) : IVocabularyRepository
{
    public async Task<IReadOnlyList<string>> GetTypesAsync() =>
        await context.VocabularyEntries
            .Select(e => e.VocabularyType)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync();

    public async Task<IReadOnlyList<VocabularyEntry>> GetByTypeAsync(string vocabularyType) =>
        await context.VocabularyEntries
            .Where(e => e.VocabularyType == vocabularyType)
            .OrderBy(e => e.Key)
            .ToListAsync();

    public async Task<string?> GetValueAsync(string vocabularyType, string key)
    {
        var entry = await context.VocabularyEntries.FirstOrDefaultAsync(
            e => e.VocabularyType == vocabularyType && e.Key == key);
        return entry?.Value;
    }

    public async Task<VocabularyEntry> AddAsync(VocabularyEntry entry)
    {
        context.VocabularyEntries.Add(entry);
        await context.SaveChangesAsync();
        return entry;
    }

    public async Task UpdateAsync(VocabularyEntry entry)
    {
        var existing = await context.VocabularyEntries.FindAsync(entry.Id)
            ?? throw new InvalidOperationException($"VocabularyEntry {entry.Id} not found.");
        existing.Value = entry.Value;
        existing.Description = entry.Description;
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await context.VocabularyEntries.FindAsync(id);
        if (entity is not null)
        {
            context.VocabularyEntries.Remove(entity);
            await context.SaveChangesAsync();
        }
    }
}
