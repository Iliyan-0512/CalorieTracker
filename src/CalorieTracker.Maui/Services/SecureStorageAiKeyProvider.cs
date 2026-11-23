using CalorieTracker.Core.Services;

namespace CalorieTracker.Maui.Services;

/// <summary>Пази Anthropic API ключа в защитеното хранилище на устройството (Keychain / Keystore / DPAPI).</summary>
public class SecureStorageAiKeyProvider : IAiKeyProvider
{
    private const string Key = "anthropic_api_key";

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
}
