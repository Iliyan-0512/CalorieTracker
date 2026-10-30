using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CalorieTracker.Core.Models;

namespace CalorieTracker.Core.Services;

public interface IBarcodeLookup
{
    Task<Food?> LookupAsync(string barcode, CancellationToken ct = default);
}

/// <summary>Безплатна отворена база OpenFoodFacts (https://world.openfoodfacts.org).</summary>
public class OpenFoodFactsClient(HttpClient http) : IBarcodeLookup
{
    public const string BaseAddress = "https://world.openfoodfacts.org/";

    // OpenFoodFacts изисква описателен User-Agent
    public const string UserAgent = "CalorieTracker/1.0 (personal nutrition tracker)";

    public static void Configure(HttpClient http)
    {
        http.BaseAddress = new Uri(BaseAddress);
        http.Timeout = TimeSpan.FromSeconds(10);
        http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    }

    public async Task<Food?> LookupAsync(string barcode, CancellationToken ct = default)
    {
        if (!barcode.All(char.IsDigit)) return null;

        try
        {
            var response = await http.GetFromJsonAsync<OffResponse>(
                $"api/v2/product/{barcode}?fields=product_name,brands,nutriments,serving_quantity", ct);

            var product = response?.Product;
            if (response?.Status != 1 || product?.Nutriments is null || string.IsNullOrWhiteSpace(product.ProductName))
                return null;

            var n = product.Nutriments;
            return new Food
            {
                Name = product.ProductName.Trim(),
                Brand = product.Brands?.Split(',')[0].Trim(),
                Barcode = barcode,
                Calories = n.EnergyKcal ?? 0,
                Protein = n.Proteins ?? 0,
                Carbs = n.Carbohydrates ?? 0,
                Fat = n.Fat ?? 0,
                Fiber = n.Fiber ?? 0,
                DefaultServingGrams = product.ServingQuantity is > 0 ? product.ServingQuantity.Value : 100,
                Source = FoodSource.OpenFoodFacts
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Без интернет или при грешка в услугата просто връщаме "не е намерено"
            return null;
        }
    }

    private sealed class OffResponse
    {
        [JsonPropertyName("status")] public int Status { get; set; }
        [JsonPropertyName("product")] public OffProduct? Product { get; set; }
    }

    private sealed class OffProduct
    {
        [JsonPropertyName("product_name")] public string? ProductName { get; set; }
        [JsonPropertyName("brands")] public string? Brands { get; set; }

        [JsonPropertyName("serving_quantity")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public double? ServingQuantity { get; set; }

        [JsonPropertyName("nutriments")] public OffNutriments? Nutriments { get; set; }
    }

    private sealed class OffNutriments
    {
        [JsonPropertyName("energy-kcal_100g")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public double? EnergyKcal { get; set; }

        [JsonPropertyName("proteins_100g")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public double? Proteins { get; set; }

        [JsonPropertyName("carbohydrates_100g")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public double? Carbohydrates { get; set; }

        [JsonPropertyName("fat_100g")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public double? Fat { get; set; }

        [JsonPropertyName("fiber_100g")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public double? Fiber { get; set; }
    }
}
