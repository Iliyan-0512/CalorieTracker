using CalorieTracker.Core.Services;

namespace CalorieTracker.Api;

/// <summary>Чете ключа от "Anthropic:ApiKey" (user-secrets / appsettings) или от променливата ANTHROPIC_API_KEY.</summary>
public class ConfigurationAiKeyProvider(IConfiguration configuration) : IAiKeyProvider
{
    public Task<string?> GetApiKeyAsync() =>
        Task.FromResult(configuration["Anthropic:ApiKey"] ?? configuration["ANTHROPIC_API_KEY"]);

    /// <summary>"Anthropic:Model" в конфигурацията; по подразбиране Haiku 5.5.</summary>
    public Task<string> GetModelAsync() =>
        Task.FromResult(configuration["Anthropic:Model"] ?? ClaudeFoodEstimator.DefaultModel);
}
