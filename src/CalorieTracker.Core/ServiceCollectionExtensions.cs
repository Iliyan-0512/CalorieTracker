using CalorieTracker.Core.Data;
using CalorieTracker.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CalorieTracker.Core;

public static class ServiceCollectionExtensions
{
    /// <summary>Регистрира базата (SQLite) и всички услуги. Ползва се и от API-то, и от мобилното приложение.</summary>
    public static IServiceCollection AddCalorieTracker(this IServiceCollection services, string sqliteConnectionString,
        string? usdaApiKey = null)
    {
        services.AddDbContext<CalorieDbContext>(options => options.UseSqlite(sqliteConnectionString));

        services.AddHttpClient<OpenFoodFactsClient>(OpenFoodFactsClient.Configure);
        services.AddTransient<IBarcodeLookup>(sp => sp.GetRequiredService<OpenFoodFactsClient>());
        services.AddTransient<IOnlineFoodSearch>(sp => sp.GetRequiredService<OpenFoodFactsClient>());

        services.AddSingleton(new UsdaOptions { ApiKey = string.IsNullOrWhiteSpace(usdaApiKey) ? "DEMO_KEY" : usdaApiKey });
        services.AddHttpClient<UsdaFoodDataClient>(UsdaFoodDataClient.Configure);
        services.AddTransient<IOnlineFoodSearch>(sp => sp.GetRequiredService<UsdaFoodDataClient>());

        // AI оценка – ключът идва от IAiKeyProvider, който всяко приложение регистрира само
        services.AddTransient<IFoodAiEstimator, ClaudeFoodEstimator>();

        services.AddScoped<FoodService>();
        services.AddScoped<ProfileService>();
        services.AddScoped<DiaryService>();
        services.AddScoped<WeightService>();
        services.AddScoped<StatsService>();
        return services;
    }

    /// <summary>Създава базата и зарежда началните храни, ако още не съществува.</summary>
    public static async Task InitializeCalorieDatabaseAsync(this IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CalorieDbContext>();
        await db.Database.EnsureCreatedAsync();
    }
}
