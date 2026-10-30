using CalorieTracker.Core;
using CalorieTracker.Maui.Services;
using CalorieTracker.Maui.ViewModels;
using CalorieTracker.Maui.Views;
using Microsoft.Extensions.Logging;

namespace CalorieTracker.Maui;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		// Локална SQLite база – приложението работи изцяло офлайн, без акаунт
		var dbPath = Path.Combine(FileSystem.AppDataDirectory, "calorietracker.db");
		builder.Services.AddCalorieTracker($"Data Source={dbPath}");
		builder.Services.AddSingleton<CoreServices>();

		builder.Services.AddTransient<DiaryViewModel>();
		builder.Services.AddTransient<AddFoodViewModel>();
		builder.Services.AddTransient<CustomFoodViewModel>();
		builder.Services.AddTransient<GoalsViewModel>();
		builder.Services.AddTransient<ProgressViewModel>();

		builder.Services.AddTransient<TodayPage>();
		builder.Services.AddTransient<AddFoodPage>();
		builder.Services.AddTransient<CustomFoodPage>();
		builder.Services.AddTransient<GoalsPage>();
		builder.Services.AddTransient<ProgressPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		var app = builder.Build();

		// Създава базата и зарежда началните храни при първо стартиране
		Task.Run(() => app.Services.InitializeCalorieDatabaseAsync()).GetAwaiter().GetResult();

		return app;
	}
}
