using CalorieTracker.Core.Services;

namespace CalorieTracker.Maui.Services;

/// <summary>Пази Anthropic API ключа в защитеното хранилище на устройството (Keychain / Keystore / DPAPI).</summary>
public class SecureStorageAiKeyProvider : IAiKeyProvider
{
    private const string Key = "anthropic_api_key";
    private const string ModelKey = "ai_model";

    public async Task<string?> GetApiKeyAsync()
    {
        try
        {
            return await SecureStorage.Default.GetAsync(Key);
        }
        catch (Exception)
        {
            // Хранилището може да е недостъпно (напр. след възстановяване от backup) – третираме като "няма ключ"
            return null;
        }
    }

    public Task SaveAsync(string apiKey) => SecureStorage.Default.SetAsync(Key, apiKey.Trim());

    public void Remove() => SecureStorage.Default.Remove(Key);

    // Моделът не е тайна, затова стои в обикновените настройки
    public string Model
    {
        get => Preferences.Default.Get(ModelKey, ClaudeFoodEstimator.DefaultModel);
        set => Preferences.Default.Set(ModelKey, value);
    }

    public Task<string> GetModelAsync() => Task.FromResult(Model);
}
