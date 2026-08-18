namespace PortfolioCalc.Core.Domain;

/// <summary>One key/value row in a small, user-editable lookup table ("vocabulary"),
/// e.g. mapping an IBKR listing-exchange code to a Yahoo Finance ticker suffix. A single
/// generic table (discriminated by <see cref="VocabularyType"/>) backs every such
/// vocabulary instead of one bespoke table per mapping — see doc/decisions.md and
/// <see cref="VocabularyTypes"/> for the current set of types. CRUD'd directly from the
/// Gui's Vocabularies page (plain CRUD, no Application-layer logic involved).</summary>
public class VocabularyEntry
{
    public int Id { get; set; }

    /// <summary>Which vocabulary this row belongs to — see <see cref="VocabularyTypes"/>.</summary>
    public required string VocabularyType { get; set; }

    /// <summary>The lookup key within this vocabulary (e.g. "LSEETF"). Unique per
    /// VocabularyType.</summary>
    public required string Key { get; set; }

    /// <summary>The mapped value (e.g. ".L"). An empty string is a valid, meaningful
    /// value (e.g. "no suffix needed"), distinct from the key being absent entirely.</summary>
    public required string Value { get; set; }

    /// <summary>Optional free-text note shown in the CRUD UI (e.g. "London Stock
    /// Exchange ETFs").</summary>
    public string? Description { get; set; }
}
