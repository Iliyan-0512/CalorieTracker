using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CalorieTracker.Core.Models;
using CalorieTracker.Core.Nutrition;
using CalorieTracker.Core.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CalorieTracker.Tests;

public class ApiTests : IClassFixture<ApiTests.Factory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client;

    public ApiTests(Factory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Foods_SearchFindsSeededCyrillicFoodsCaseInsensitively()
    {
        var foods = await _client.GetFromJsonAsync<List<Food>>("/api/foods?search=КИСЕЛО", Json);
        Assert.Contains(foods!, f => f.Name == "Кисело мляко 2%");
    }

    [Fact]
    public async Task Barcode_FallsBackToLookupAndCachesLocally()
    {
        var food = await _client.GetFromJsonAsync<Food>("/api/foods/barcode/1234567890123", Json);
        Assert.Equal("Тестов продукт", food!.Name);
        Assert.Equal(FoodSource.OpenFoodFacts, food.Source);

        var again = await _client.GetFromJsonAsync<Food>("/api/foods/barcode/1234567890123", Json);
        Assert.Equal(food.Id, again!.Id);

        var missing = await _client.GetAsync("/api/foods/barcode/0000000000000");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Meals_LogAndReadDailySummary()
    {
        var date = new DateOnly(2026, 1, 15);

        var custom = await _client.PostAsJsonAsync("/api/foods",
            new NewFood("Тестова храна", null, null, 200, 10, 20, 5, 1), Json);
        Assert.Equal(HttpStatusCode.Created, custom.StatusCode);
        var food = await custom.Content.ReadFromJsonAsync<Food>(Json);

        var logged = await _client.PostAsJsonAsync("/api/meals",
            new LogMealRequest(date, MealType.Lunch, food!.Id, 150), Json);
        Assert.Equal(HttpStatusCode.Created, logged.StatusCode);

        var summary = await _client.GetFromJsonAsync<DailySummary>($"/api/meals?date={date:yyyy-MM-dd}", Json);
        Assert.Equal(300, summary!.Consumed.Calories);
        Assert.Equal(15, summary.Consumed.Protein);
        Assert.Equal(summary.Targets.Calories - 300, summary.Remaining.Calories);
        Assert.Single(summary.Meals.Single(m => m.MealType == MealType.Lunch).Entries);
    }

    [Fact]
    public async Task Meals_RejectInvalidGramsAndUnknownFood()
    {
        var badGrams = await _client.PostAsJsonAsync("/api/meals",
            new LogMealRequest(new DateOnly(2026, 1, 1), MealType.Snack, 1, -5), Json);
        Assert.Equal(HttpStatusCode.BadRequest, badGrams.StatusCode);

        var unknown = await _client.PostAsJsonAsync("/api/meals",
            new LogMealRequest(new DateOnly(2026, 1, 1), MealType.Snack, 999_999, 100), Json);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task Profile_RejectsMacroSplitNotSummingTo100()
    {
        var response = await _client.PutAsJsonAsync("/api/profile",
            new UserProfile { CarbsPercent = 60, ProteinPercent = 30, FatPercent = 30 }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Stats_CountLoggedDaysAndWeightChange()
    {
        var to = new DateOnly(2025, 6, 7);
        await _client.PostAsJsonAsync("/api/weights", new LogWeightRequest(to.AddDays(-6), 80), Json);
        await _client.PostAsJsonAsync("/api/weights", new LogWeightRequest(to, 79.2), Json);
        await _client.PostAsJsonAsync("/api/meals", new LogMealRequest(to, MealType.Dinner, 3, 200), Json);

        var stats = await _client.GetFromJsonAsync<Stats>($"/api/stats?days=7&to={to:yyyy-MM-dd}", Json);

        Assert.Equal(7, stats!.Days.Count);
        Assert.Equal(1, stats.DaysLogged);
        Assert.Equal(260, stats.AverageCalories);
        Assert.Equal(-0.8, stats.WeightChangeKg);
    }

    [Fact]
    public async Task Foods_UpdateChangesCaloriesAndRecalculatesDiary()
    {
        var date = new DateOnly(2024, 3, 3);
        var created = await (await _client.PostAsJsonAsync("/api/foods",
            new NewFood("Моя салата", null, null, 100, 2, 5, 8, 2), Json)).Content.ReadFromJsonAsync<Food>(Json);
        await _client.PostAsJsonAsync("/api/meals", new LogMealRequest(date, MealType.Lunch, created!.Id, 200), Json);

        var update = await _client.PutAsJsonAsync($"/api/foods/{created.Id}",
            new NewFood("Моя салата", null, null, 150, 2, 5, 12, 2), Json);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var summary = await _client.GetFromJsonAsync<DailySummary>($"/api/meals?date={date:yyyy-MM-dd}", Json);
        Assert.Equal(300, summary!.Consumed.Calories);

        var missing = await _client.PutAsJsonAsync("/api/foods/999999", new NewFood("X", null, null, 1, 0, 0, 0, 0), Json);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Foods_OnlineSearchDeduplicatesAndImportSavesOnce()
    {
        var online = await _client.GetFromJsonAsync<List<Food>>("/api/foods/online?search=вафла", Json);
        var food = Assert.Single(online!);
        Assert.Equal(0, food.Id);

        var first = await (await _client.PostAsJsonAsync("/api/foods/import", food, Json)).Content.ReadFromJsonAsync<Food>(Json);
        var second = await (await _client.PostAsJsonAsync("/api/foods/import", food, Json)).Content.ReadFromJsonAsync<Food>(Json);
        Assert.NotEqual(0, first!.Id);
        Assert.Equal(first.Id, second!.Id);
    }

    [Fact]
    public async Task Ai_EstimateReturnsItems()
    {
        var response = await _client.PostAsJsonAsync("/api/ai/estimate", new AiTextRequest("мусака 300 г"), Json);
        var items = await response.Content.ReadFromJsonAsync<List<AiFoodEstimate>>(Json);
        var item = Assert.Single(items!);
        Assert.Equal(300, item.Grams);
        Assert.Equal(540, item.ToFood().ForGrams(item.Grams).Calories);
    }

    public class Factory : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"calorietracker-test-{Guid.NewGuid():N}.db");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath}");
            builder.ConfigureTestServices(services =>
            {
                // Тестовете не трябва да ходят в интернет или да харчат AI заявки
                services.RemoveAll<IBarcodeLookup>();
                services.RemoveAll<IOnlineFoodSearch>();
                services.RemoveAll<IFoodAiEstimator>();
                services.AddSingleton<IBarcodeLookup, FakeBarcodeLookup>();
                services.AddSingleton<IOnlineFoodSearch, FakeOnlineSearch>();
                services.AddSingleton<IFoodAiEstimator, FakeAiEstimator>();
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            SqliteConnection.ClearAllPools();
            File.Delete(_dbPath);
        }
    }

    private class FakeOnlineSearch : IOnlineFoodSearch
    {
        public Task<IReadOnlyList<Food>> SearchAsync(string query, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Food>>(
            [
                new Food { Name = $"Онлайн {query}", Barcode = "5555555555555", Calories = 250, Protein = 10, Carbs = 30, Fat = 8, Source = FoodSource.OpenFoodFacts },
                new Food { Name = $"Онлайн {query}", Barcode = "5555555555555", Calories = 250, Protein = 10, Carbs = 30, Fat = 8, Source = FoodSource.OpenFoodFacts },
            ]);
    }

    private class FakeAiEstimator : IFoodAiEstimator
    {
        public Task<bool> IsConfiguredAsync() => Task.FromResult(true);

        public Task<IReadOnlyList<AiFoodEstimate>> EstimateFromTextAsync(string description, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AiFoodEstimate>>([new AiFoodEstimate("Мусака", 300, 180, 9, 12, 10, 1.5, "medium", null)]);

        public Task<IReadOnlyList<AiFoodEstimate>> EstimateFromPhotoAsync(byte[] image, string mediaType, string? hint, CancellationToken ct = default) =>
            EstimateFromTextAsync("снимка", ct);
    }

    private class FakeBarcodeLookup : IBarcodeLookup
    {
        public Task<Food?> LookupAsync(string barcode, CancellationToken ct = default) =>
            Task.FromResult(barcode == "1234567890123"
                ? new Food
                {
                    Name = "Тестов продукт", Barcode = barcode, Calories = 100, Protein = 5, Carbs = 10, Fat = 2,
                    Source = FoodSource.OpenFoodFacts
                }
                : null);
    }
}
