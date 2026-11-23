using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CalorieTracker.Core.Models;

namespace CalorieTracker.Core.Services;

/// <summary>
/// USDA FoodData Central (https://fdc.nal.usda.gov) – лабораторни данни за ~8 000 основни храни.
/// Работи само с английски имена. DEMO_KEY има нисък лимит; безплатен ключ: https://fdc.nal.usda.gov/api-key-signup
/// </summary>
public class UsdaFoodDataClient(HttpClient http, UsdaOptions options) : IOnlineFoodSearch
{
    public const string BaseAddress = "https://api.nal.usda.gov/fdc/v1/";

    // Номера на нутриентите в FoodData Central
    private const int EnergyKcal = 1008, EnergyAtwaterGeneral = 2047, EnergyAtwaterSpecific = 2048;
    private const int Protein = 1003, Fat = 1004, Carbs = 1005, Fiber = 1079;

    public static void Configure(HttpClient http)
    {
        http.BaseAddress = new Uri(BaseAddress);
        http.Timeout = TimeSpan.FromSeconds(12);
    }

    public async Task<IReadOnlyList<Food>> SearchAsync(string query, CancellationToken ct = default)
    {
        // USDA няма данни на кирилица – такива заявки ги оставяме за OpenFoodFacts и AI
        if (query.Any(c => c is >= 'А' and <= 'я')) return [];

        var url = $"foods/search?query={Uri.EscapeDataString(query)}&pageSize=20" +
                  $"&dataType=Foundation&dataType=SR%20Legacy&api_key={Uri.EscapeDataString(options.ApiKey)}";
        var response = await http.GetFromJsonAsync<UsdaSearchResponse>(url, ct);

        return response?.Foods?
            .Select(ToFood)
            .OfType<Food>()
            .ToList() ?? [];
    }

    private static Food? ToFood(UsdaFood f)
    {
        if (string.IsNullOrWhiteSpace(f.Description) || f.FoodNutrients is null) return null;

        double? Value(params int[] ids) =>
            ids.Select(id => f.FoodNutrients.FirstOrDefault(n => n.NutrientId == id)?.Value).FirstOrDefault(v => v is not null);

        var kcal = Value(EnergyKcal, EnergyAtwaterSpecific, EnergyAtwaterGeneral);
        if (kcal is null) return null;

        return new Food
        {
            Name = f.Description.Trim(),
            Calories = kcal.Value,
            Protein = Value(Protein) ?? 0,
            Carbs = Value(Carbs) ?? 0,
            Fat = Value(Fat) ?? 0,
            Fiber = Value(Fiber) ?? 0,
            DefaultServingGrams = 100,
            Source = FoodSource.Usda
        };
    }

    private sealed class UsdaSearchResponse
    {
        [JsonPropertyName("foods")] public List<UsdaFood>? Foods { get; set; }
    }

    private sealed class UsdaFood
    {
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("foodNutrients")] public List<UsdaNutrient>? FoodNutrients { get; set; }
    }

    private sealed class UsdaNutrient
    {
        [JsonPropertyName("nutrientId")] public int NutrientId { get; set; }
        [JsonPropertyName("value")] public double? Value { get; set; }
    }
}

public class UsdaOptions
{
    public string ApiKey { get; set; } = "DEMO_KEY";
}
