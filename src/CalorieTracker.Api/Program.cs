using System.Text.Json.Serialization;
using CalorieTracker.Api;
using CalorieTracker.Core;
using CalorieTracker.Core.Models;
using CalorieTracker.Core.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddCalorieTracker(
    builder.Configuration.GetConnectionString("Default") ?? "Data Source=calorietracker.db");

var app = builder.Build();

await app.Services.InitializeCalorieDatabaseAsync();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseHttpsRedirection();

app.MapGet("/", () => Results.Redirect("/health"));
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

var api = app.MapGroup("/api");

// ---- Храни ----
var foods = api.MapGroup("/foods").WithTags("Foods");

foods.MapGet("/", (string? search, int? limit, FoodService service, CancellationToken ct) =>
    service.SearchAsync(search, Math.Clamp(limit ?? 50, 1, 200), ct));

foods.MapGet("/{id:int}", async (int id, FoodService service, CancellationToken ct) =>
    await service.GetAsync(id, ct) is { } food ? Results.Ok(food) : Results.NotFound());

foods.MapGet("/barcode/{code}", async (string code, FoodService service, CancellationToken ct) =>
    await service.FindByBarcodeAsync(code, ct) is { } food ? Results.Ok(food) : Results.NotFound());

foods.MapGet("/recent", (int? limit, DiaryService service, CancellationToken ct) =>
    service.GetRecentFoodsAsync(Math.Clamp(limit ?? 10, 1, 50), ct));

foods.MapPost("/", async (NewFood input, FoodService service, CancellationToken ct) =>
{
    var food = await service.AddCustomAsync(input, ct);
    return Results.Created($"/api/foods/{food.Id}", food);
});

// ---- Дневник (хранения) ----
var meals = api.MapGroup("/meals").WithTags("Meals");

meals.MapGet("/", (DateOnly? date, DiaryService service, CancellationToken ct) =>
    service.GetDayAsync(date ?? DateOnly.FromDateTime(DateTime.Today), ct));

meals.MapPost("/", async (LogMealRequest request, DiaryService service, CancellationToken ct) =>
{
    var entry = await service.AddEntryAsync(request.Date, request.MealType, request.FoodId, request.Grams, ct);
    return Results.Created($"/api/meals/{entry.Id}", entry);
});

meals.MapPut("/{id:int}", async (int id, UpdateMealRequest request, DiaryService service, CancellationToken ct) =>
    await service.UpdateGramsAsync(id, request.Grams, ct) ? Results.NoContent() : Results.NotFound());

meals.MapDelete("/{id:int}", async (int id, DiaryService service, CancellationToken ct) =>
    await service.DeleteEntryAsync(id, ct) ? Results.NoContent() : Results.NotFound());

// ---- Профил и цели ----
var profile = api.MapGroup("/profile").WithTags("Profile");

profile.MapGet("/", (ProfileService service, CancellationToken ct) => service.GetAsync(ct));
profile.MapPut("/", (UserProfile input, ProfileService service, CancellationToken ct) => service.SaveAsync(input, ct));
profile.MapGet("/targets", (ProfileService service, CancellationToken ct) => service.GetTargetsAsync(ct));

// ---- Тегло ----
var weights = api.MapGroup("/weights").WithTags("Weights");

weights.MapGet("/", (DateOnly? from, DateOnly? to, WeightService service, CancellationToken ct) =>
{
    var end = to ?? DateOnly.FromDateTime(DateTime.Today);
    return service.GetHistoryAsync(from ?? end.AddDays(-90), end, ct);
});

weights.MapPost("/", async (LogWeightRequest request, WeightService service, CancellationToken ct) =>
    Results.Ok(await service.LogAsync(request.Date, request.WeightKg, ct)));

weights.MapDelete("/{id:int}", async (int id, WeightService service, CancellationToken ct) =>
    await service.DeleteAsync(id, ct) ? Results.NoContent() : Results.NotFound());

// ---- Статистика ----
api.MapGet("/stats", (int? days, DateOnly? to, StatsService service, CancellationToken ct) =>
        service.GetAsync(to ?? DateOnly.FromDateTime(DateTime.Today), days ?? 7, ct))
    .WithTags("Stats");

app.Run();

public record LogMealRequest(DateOnly Date, MealType MealType, int FoodId, double Grams);

public record UpdateMealRequest(double Grams);

public record LogWeightRequest(DateOnly Date, double WeightKg);

public partial class Program;
