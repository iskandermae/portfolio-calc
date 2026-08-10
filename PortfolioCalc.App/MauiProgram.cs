using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PortfolioCalc.Core.Data;

namespace PortfolioCalc.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder.UseMauiApp<App>();

		builder.Services.AddMauiBlazorWebView();

		Directory.CreateDirectory(PortfolioDbContext.DefaultDataDirectory);
		builder.Services.AddDbContext<PortfolioDbContext>(options =>
			options.UseSqlite($"Data Source={PortfolioDbContext.DefaultDatabasePath}"));

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		var app = builder.Build();

		using (var scope = app.Services.CreateScope())
		{
			scope.ServiceProvider.GetRequiredService<PortfolioDbContext>().Database.EnsureCreated();
		}

		return app;
	}
}
