using ApexCharts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PortfolioCalc.App.Application.Fx;
using PortfolioCalc.App.Application.Import.Ibkr;
using PortfolioCalc.App.Application.Inflation;
using PortfolioCalc.App.Application.Positions;
using PortfolioCalc.App.Application.Prices;
using PortfolioCalc.App.Application.Tax;
using PortfolioCalc.App.Application.Transactions;
using PortfolioCalc.App.Logging;
using PortfolioCalc.Core.Data;
using PortfolioCalc.Core.Data.Fx;
using PortfolioCalc.Core.Data.Import.Ibkr;
using PortfolioCalc.Core.Data.Inflation;
using PortfolioCalc.Core.Data.Prices;
using PortfolioCalc.Core.Data.Repositories;
using PortfolioCalc.Core.Fx;
using PortfolioCalc.Core.Import;
using PortfolioCalc.Core.Inflation;
using PortfolioCalc.Core.Prices;
using PortfolioCalc.Core.Repositories;

namespace PortfolioCalc.App;

public static class MauiProgram
{
	/// <summary>Passed on the command line (e.g. `dotnet run --project PortfolioCalc.App --
	/// --reset-db`) to delete the local database before startup — an escape hatch for a
	/// database left behind by a schema change with no corresponding migration, without
	/// requiring the user to go find and delete the file by hand.</summary>
	private const string ResetDbArgument = "--reset-db";

	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder.UseMauiApp<App>();

		builder.Services.AddMauiBlazorWebView();
		builder.Services.AddApexChartsMaui();

		Directory.CreateDirectory(PortfolioDbContext.DefaultDataDirectory);

		if (Environment.GetCommandLineArgs().Contains(ResetDbArgument, StringComparer.OrdinalIgnoreCase))
			DeleteDatabaseFiles();

		builder.Services.AddDbContext<PortfolioDbContext>(options =>
			options.UseSqlite($"Data Source={PortfolioDbContext.DefaultDatabasePath}"));

		builder.Services.AddScoped<IAccountRepository, AccountRepository>();
		builder.Services.AddScoped<ISecurityRepository, SecurityRepository>();
		builder.Services.AddScoped<IPositionRepository, PositionRepository>();
		builder.Services.AddScoped<ICashTransactionRepository, CashTransactionRepository>();
		builder.Services.AddScoped<ISecurityTransactionRepository, SecurityTransactionRepository>();
		builder.Services.AddScoped<ITransactionImporter, IbkrFlexQueryImporter>();
		builder.Services.AddScoped<IbkrImportService>();

		builder.Services.AddScoped<IFxRateRepository, FxRateRepository>();
		builder.Services.AddHttpClient<FrankfurterFxRateProvider>();
		builder.Services.AddHttpClient<NbuFxRateProvider>();
		// Frankfurter doesn't cover UAH (story 12); the composite routes a UAH-involving
		// pair to the NBU-backed provider and everything else to Frankfurter — see
		// doc/decisions.md.
		builder.Services.AddScoped<IFxRateProvider>(sp => new CompositeFxRateProvider(
			sp.GetRequiredService<FrankfurterFxRateProvider>(), sp.GetRequiredService<NbuFxRateProvider>()));
		builder.Services.AddScoped<FxRateService>();

		builder.Services.AddScoped<ISecurityPriceRepository, SecurityPriceRepository>();
		builder.Services.AddHttpClient<ISecurityPriceProvider, YahooFinanceSecurityPriceProvider>();
		builder.Services.AddScoped<SecurityPriceService>();

		builder.Services.AddScoped<IAppSettingsRepository, AppSettingsRepository>();
		builder.Services.AddScoped<BaseCurrencyConversionService>();
		builder.Services.AddScoped<PositionValuationService>();
		builder.Services.AddScoped<PositionValueChartService>();

		builder.Services.AddScoped<IUiLayoutSettingRepository, UiLayoutSettingRepository>();
		builder.Services.AddScoped<IVocabularyRepository, VocabularyRepository>();

		builder.Services.AddScoped<IInflationRateRepository, InflationRateRepository>();
		builder.Services.AddHttpClient<WorldBankInflationRateProvider>();
		// Wrapped so a "InflationRateOverride" Vocabularies entry can fill a gap (e.g. the
		// current year's CPI not published yet) — see doc/decisions.md.
		builder.Services.AddScoped<IInflationRateProvider>(sp => new VocabularyOverrideInflationRateProvider(
			sp.GetRequiredService<WorldBankInflationRateProvider>(), sp.GetRequiredService<IVocabularyRepository>(),
			sp.GetRequiredService<ILogger<VocabularyOverrideInflationRateProvider>>()));
		builder.Services.AddScoped<InflationRateService>();
		builder.Services.AddScoped<PositionPerformanceService>();
		builder.Services.AddScoped<TransactionPerformanceService>();
		builder.Services.AddScoped<TransactionDeleteService>();
		builder.Services.AddScoped<TaxEstimationService>();
		builder.Services.AddScoped<PortfolioCalc.App.Gui.State.TaxEstimationPageState>();

		// App-wide file logging so a non-developer user has somewhere to look (the Logs
		// page) instead of a page silently going blank on an unhandled exception — see
		// doc/decisions.md. LogActivityTracker is constructed once here and registered as
		// a singleton too, so the Gui (NavMenu/Logs page) shares the exact same instance
		// the file logger reports into.
		var logActivityTracker = new LogActivityTracker();
		builder.Services.AddSingleton(logActivityTracker);
		builder.Logging.AddProvider(
			new FileLoggerProvider(Path.Combine(PortfolioDbContext.DefaultDataDirectory, "log.txt"), logActivityTracker));

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		var app = builder.Build();

		using (var scope = app.Services.CreateScope())
		{
			scope.ServiceProvider.GetRequiredService<PortfolioDbContext>().Database.Migrate();
		}

		// Belt-and-suspenders: a page-render exception is caught and logged by
		// LoggingErrorBoundary (Routes.razor), but this catches anything outside that —
		// e.g. an exception on a background thread never awaited by a component — so it
		// still reaches log.txt instead of vanishing. See doc/decisions.md.
		var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("UnhandledException");
		AppDomain.CurrentDomain.UnhandledException += (_, e) => startupLogger.LogCritical(
			e.ExceptionObject as Exception, "Unhandled AppDomain exception (IsTerminating={IsTerminating}).", e.IsTerminating);
		TaskScheduler.UnobservedTaskException += (_, e) =>
		{
			startupLogger.LogError(e.Exception, "Unobserved task exception.");
			e.SetObserved();
		};

		return app;
	}

	private static void DeleteDatabaseFiles()
	{
		// SQLite can leave -wal/-shm sidecar files alongside the main database file;
		// delete whatever's actually there rather than assuming just the one file.
		var dbPath = PortfolioDbContext.DefaultDatabasePath;
		foreach (var path in new[] { dbPath, $"{dbPath}-wal", $"{dbPath}-shm" })
		{
			if (File.Exists(path))
				File.Delete(path);
		}
	}
}
