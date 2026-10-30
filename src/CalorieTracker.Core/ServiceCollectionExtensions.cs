using CalorieTracker.Core.Data;
using CalorieTracker.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CalorieTracker.Core;

public static class ServiceCollectionExtensions
{
    /// <summary>Регистрира базата (SQLite) и всички услуги. Ползва се и от API-то, и от мобилното приложение.</summary>
    public static IServiceCollection AddCalorieTracker(this IServiceCollection services, string sqliteConnectionString)
    {
        services.AddDbContext<CalorieDbContext>(options => options.UseSqlite(sqliteConnectionString));
        services.AddHttpClient<IBarcodeLookup, OpenFoodFactsClient>(OpenFoodFactsClient.Configure);

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
