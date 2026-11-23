using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CalorieTracker.Core.Models;

namespace CalorieTracker.Core.Services;

public interface IBarcodeLookup
{
    Task<Food?> LookupAsync(string barcode, CancellationToken ct = default);
}

/// <summary>Онлайн база с храни. Резултатите са със стойности на 100 g и не са записани локално.</summary>
public interface IOnlineFoodSearch
{
    Task<IReadOnlyList<Food>> SearchAsync(string query, CancellationToken ct = default);
}

/// <summary>Безплатна отворена база OpenFoodFacts (https://world.openfoodfacts.org) – над 3 млн. продукта, вкл. български.</summary>
public class OpenFoodFactsClient(HttpClient http) : IBarcodeLookup, IOnlineFoodSearch
{
    public const string BaseAddress = "https://world.openfoodfacts.org/";

    // OpenFoodFacts изисква описателен User-Agent
    public const string UserAgent = "CalorieTracker/1.0 (personal nutrition tracker)";

    private const string Fields = "code,product_name,product_name_bg,brands,nutriments,serving_quantity";

    public static void Configure(HttpClient http)
    {
        http.BaseAddress = new Uri(BaseAddress);
        http.Timeout = TimeSpan.FromSeconds(12);
        http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    }

    public async Task<Food?> LookupAsync(string barcode, CancellationToken ct = default)
    {
        if (!barcode.All(char.IsDigit)) return null;

        try
        {
            var response = await http.GetFromJsonAsync<OffResponse>($"api/v2/product/{barcode}?fields={Fields}", ct);
            return response?.Status == 1 ? ToFood(response.Product, barcode) : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Без интернет или при грешка в услугата просто връщаме "не е намерено"
            return null;
        }
    }

    public async Task<IReadOnlyList<Food>> SearchAsync(string query, CancellationToken ct = default)
    {
        var url = $"cgi/search.pl?search_terms={Uri.EscapeDataString(query)}&search_simple=1&action=process&json=1&page_size=25&fields={Fields}";
        var response = await http.GetFromJsonAsync<OffSearchResponse>(url, ct);

        return response?.Products?
            .Select(p => ToFood(p, p.Code))
            .OfType<Food>()
            .ToList() ?? [];
    }

    private static Food? ToFood(OffProduct? product, string? barcode)
    {
        var name = !string.IsNullOrWhiteSpace(product?.ProductNameBg) ? product.ProductNameBg : product?.ProductName;
        var n = product?.Nutriments;
        if (string.IsNullOrWhiteSpace(name) || n?.EnergyKcal is null) return null;

        return new Food
        {
            Name = name.Trim(),
            Brand = product!.Brands?.Split(',')[0].Trim() is { Length: > 0 } brand ? brand : null,
            Barcode = string.IsNullOrWhiteSpace(barcode) ? null : barcode,
            Calories = n.EnergyKcal.Value,
            Protein = n.Proteins ?? 0,
            Carbs = n.Carbohydrates ?? 0,
            Fat = n.Fat ?? 0,
            Fiber = n.Fiber ?? 0,
            DefaultServingGrams = product.ServingQuantity is > 0 ? product.ServingQuantity.Value : 100,
            Source = FoodSource.OpenFoodFacts
        };
    }

    private sealed class OffResponse
    {
        [JsonPropertyName("status")] public int Status { get; set; }
        [JsonPropertyName("product")] public OffProduct? Product { get; set; }
    }

    private sealed class OffSearchResponse
    {
        [JsonPropertyName("products")] public List<OffProduct>? Products { get; set; }
    }

    private sealed class OffProduct
    {
        [JsonPropertyName("code")] public string? Code { get; set; }
        [JsonPropertyName("product_name")] public string? ProductName { get; set; }
        [JsonPropertyName("product_name_bg")] public string? ProductNameBg { get; set; }
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
