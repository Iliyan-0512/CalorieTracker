namespace CalorieTracker.Maui.Services;

/// <summary>
/// Услугите от Core работят с DbContext, който трябва да е краткоживущ.
/// Всяко действие в UI получава собствен scope, за да не държим стари данни в паметта.
/// </summary>
public class CoreServices(IServiceScopeFactory scopeFactory)
{
    public async Task<T> RunAsync<TService, T>(Func<TService, Task<T>> action) where TService : notnull
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<TService>());
    }

    public async Task RunAsync<TService>(Func<TService, Task> action) where TService : notnull
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<TService>());
    }
}
