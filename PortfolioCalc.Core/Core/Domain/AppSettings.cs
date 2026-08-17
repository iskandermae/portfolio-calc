namespace PortfolioCalc.Core.Domain;

/// <summary>Single-row app-level settings — see
/// doc/stories/05-base-currency-setting.md. Always has Id = <see cref="SingletonId"/>; no
/// history/versioning needed for the setting itself.</summary>
public class AppSettings
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public required string BaseCurrency { get; set; }
}
