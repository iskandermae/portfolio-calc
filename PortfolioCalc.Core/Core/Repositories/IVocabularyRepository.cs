using PortfolioCalc.Core.Domain;

namespace PortfolioCalc.Core.Repositories;

/// <summary>CRUD + lookup for the generic <see cref="VocabularyEntry"/> table — see
/// doc/decisions.md.</summary>
public interface IVocabularyRepository
{
    /// <summary>Distinct vocabulary types currently present, for the Gui to render one
    /// sub-tab per vocabulary without hard-coding the list.</summary>
    Task<IReadOnlyList<string>> GetTypesAsync();

    Task<IReadOnlyList<VocabularyEntry>> GetByTypeAsync(string vocabularyType);

    /// <summary>Looks up one entry's value; null if the vocabulary has no row for that
    /// key (an unmapped/unknown key, distinct from an entry whose Value is "").</summary>
    Task<string?> GetValueAsync(string vocabularyType, string key);

    Task<VocabularyEntry> AddAsync(VocabularyEntry entry);
    Task UpdateAsync(VocabularyEntry entry);
    Task DeleteAsync(int id);
}
