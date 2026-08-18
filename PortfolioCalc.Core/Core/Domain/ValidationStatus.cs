namespace PortfolioCalc.Core.Domain;

/// <summary>Manual-review status for a stored <see cref="FxRate"/> or <see
/// cref="SecurityPrice"/> — see doc/stories/07-price-rate-quality-validation.md. Only
/// <see cref="Valid"/> values are used in calculations/reports; <see
/// cref="PendingValidation"/> and <see cref="Rejected"/> values are excluded until a user
/// reviews them.</summary>
public enum ValidationStatus
{
    /// <summary>Default — either judged consistent with recent history, or stored before
    /// this status existed, or with no history yet to judge it against.</summary>
    Valid = 0,
    PendingValidation = 1,
    Rejected = 2,
}
