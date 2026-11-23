using CalorieTracker.Core;
using CalorieTracker.Maui.Services;
using CalorieTracker.Maui.ViewModels;
using CalorieTracker.Maui.Views;
using CalorieTracker.Core.Services;
using Microsoft.Extensions.Logging;
using ZXing.Net.Maui.Controls;

namespace CalorieTracker.Maui;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseBarcodeReader()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		// Локална SQLite база – приложението работи изцяло офлайн, без акаунт
		var dbPath = Path.Combine(FileSystem.AppDataDirectory, "calorietracker.db");
		builder.Services.AddCalorieTracker($"Data Source={dbPath}");
		builder.Services.AddSingleton<CoreServices>();
		builder.Services.AddSingleton<SecureStorageAiKeyProvider>();
		builder.Services.AddSingleton<IAiKeyProvider>(sp => sp.GetRequiredService<SecureStorageAiKeyProvider>());

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
		builder.Services.AddTransient<AiFoodViewModel>();
		builder.Services.AddTransient<AiFoodPage>();
		builder.Services.AddTransient<ScanBarcodePage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		var app = builder.Build();

		// Създава базата и зарежда началните храни при първо стартиране
		Task.Run(() => app.Services.InitializeCalorieDatabaseAsync()).GetAwaiter().GetResult();

		return app;
	}
}
