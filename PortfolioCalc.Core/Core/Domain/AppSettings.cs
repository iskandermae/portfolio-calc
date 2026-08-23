namespace PortfolioCalc.Core.Domain;

/// <summary>Single-row app-level settings — see
/// doc/stories/05-base-currency-setting.md. Always has Id = <see cref="SingletonId"/>; no
/// history/versioning needed for the setting itself.</summary>
public class AppSettings
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public required string BaseCurrency { get; set; }

    /// <summary>Base currency for the tax-estimation report (story 12) — a separate global
    /// setting from <see cref="BaseCurrency"/>, since gross-gain figures don't need to share
    /// the portfolio-value reports' currency. Defaults so existing call sites that only set
    /// <see cref="BaseCurrency"/> keep compiling.</summary>
    public string TaxBaseCurrency { get; set; } = TaxSupportedCurrencies.DefaultCode;
}
