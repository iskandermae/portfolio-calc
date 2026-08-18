using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PortfolioCalc.App.Application.Fx;
using PortfolioCalc.App.Application.Import.Ibkr;
using PortfolioCalc.App.Application.Inflation;
using PortfolioCalc.Core.Data;
using PortfolioCalc.Core.Data.Fx;
using PortfolioCalc.Core.Data.Import.Ibkr;
using PortfolioCalc.Core.Data.Inflation;
using PortfolioCalc.Core.Data.Repositories;
using PortfolioCalc.Core.Fx;
using PortfolioCalc.Core.Import;
using PortfolioCalc.Core.Inflation;
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
		builder.Services.AddHttpClient<IFxRateProvider, FrankfurterFxRateProvider>();
		builder.Services.AddScoped<FxRateService>();

		// Registered for the validation-review screen's direct repository reads/writes
		// (Home.razor/ValidationReview.razor) — no ISecurityPriceProvider/SecurityPriceService
		// wiring yet since no price data source has been chosen (see doc/decisions.md).
		builder.Services.AddScoped<ISecurityPriceRepository, SecurityPriceRepository>();

		builder.Services.AddScoped<IAppSettingsRepository, AppSettingsRepository>();
		builder.Services.AddScoped<BaseCurrencyConversionService>();

		builder.Services.AddScoped<IInflationRateRepository, InflationRateRepository>();
		builder.Services.AddHttpClient<IInflationRateProvider, WorldBankInflationRateProvider>();
		builder.Services.AddScoped<InflationRateService>();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		var app = builder.Build();

		using (var scope = app.Services.CreateScope())
		{
			scope.ServiceProvider.GetRequiredService<PortfolioDbContext>().Database.Migrate();
		}

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
