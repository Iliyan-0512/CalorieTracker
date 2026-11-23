using CalorieTracker.Core.Data;
using CalorieTracker.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Core.Services;

public record NewFood(string Name, string? Brand, string? Barcode, double Calories, double Protein, double Carbs,
    double Fat, double Fiber, double DefaultServingGrams = 100);

public class FoodService(
    CalorieDbContext db,
    IBarcodeLookup? barcodeLookup = null,
    IEnumerable<IOnlineFoodSearch>? onlineSearches = null)
{
    public async Task<IReadOnlyList<Food>> SearchAsync(string? query, int limit = 50, CancellationToken ct = default)
    {
        var foods = await db.Foods.AsNoTracking().ToListAsync(ct);
        query = query?.Trim();

        // Филтрираме в паметта: LIKE в SQLite не прави case-insensitive сравнение за кирилица.
        // За лична база от няколко хиляди храни това е напълно достатъчно бързо.
        IEnumerable<Food> result = string.IsNullOrEmpty(query)
            ? foods.OrderBy(f => f.Name)
            : foods
                .Where(f => f.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                            || (f.Brand?.Contains(query, StringComparison.CurrentCultureIgnoreCase) ?? false)
                            || f.Barcode == query)
                .OrderBy(f => f.Name.StartsWith(query, StringComparison.CurrentCultureIgnoreCase) ? 0 : 1)
                .ThenBy(f => f.Name);

        return result.Take(limit).ToList();
    }

    public Task<Food?> GetAsync(int id, CancellationToken ct = default) =>
        db.Foods.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, ct);

    /// <summary>Търси по баркод първо локално, после в OpenFoodFacts. Намереното се кешира локално за офлайн ползване.</summary>
    public async Task<Food?> FindByBarcodeAsync(string barcode, CancellationToken ct = default)
    {
        barcode = barcode.Trim();
        if (barcode.Length == 0) return null;

        var local = await db.Foods.AsNoTracking().FirstOrDefaultAsync(f => f.Barcode == barcode, ct);
        if (local is not null || barcodeLookup is null) return local;

        var remote = await barcodeLookup.LookupAsync(barcode, ct);
        if (remote is null) return null;

        db.Foods.Add(remote);
        await db.SaveChangesAsync(ct);
        return remote;
    }

    public async Task<Food> AddCustomAsync(NewFood input, CancellationToken ct = default)
    {
        Validate(input);

        var food = new Food { Name = input.Name.Trim(), Source = FoodSource.Custom };
        Apply(food, input);

        db.Foods.Add(food);
        await db.SaveChangesAsync(ct);
        return food;
    }

    /// <summary>
    /// Промяна на калориите/макросите на съществуваща храна (включително вградените),
    /// за да ги напаснете спрямо етикета или собствената рецепта. Записите в дневника се преизчисляват автоматично.
    /// </summary>
    public async Task<Food?> UpdateAsync(int id, NewFood input, CancellationToken ct = default)
    {
        Validate(input);

        var food = await db.Foods.FindAsync([id], ct);
        if (food is null) return null;

        Apply(food, input);
        await db.SaveChangesAsync(ct);
        return food;
    }

    /// <summary>Търси онлайн (OpenFoodFacts, USDA). Резултатите още не са записани локално (Id = 0).</summary>
    public async Task<IReadOnlyList<Food>> SearchOnlineAsync(string query, CancellationToken ct = default)
    {
        query = query.Trim();
        if (query.Length < 2 || onlineSearches is null) return [];

        var tasks = onlineSearches.Select(async s =>
        {
            try { return await s.SearchAsync(query, ct); }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
            {
                return (IReadOnlyList<Food>)[];
            }
        });

        var results = await Task.WhenAll(tasks);
        return results.SelectMany(r => r)
            .DistinctBy(f => (f.Name.ToLowerInvariant(), f.Brand?.ToLowerInvariant(), Math.Round(f.Calories)))
            .ToList();
    }

    /// <summary>Записва храна от онлайн търсене или AI в локалната база, за да може да се логне и да работи офлайн.</summary>
    public async Task<Food> SaveExternalAsync(Food food, CancellationToken ct = default)
    {
        if (food.Id != 0) return food;

        if (!string.IsNullOrWhiteSpace(food.Barcode))
        {
            var existing = await db.Foods.FirstOrDefaultAsync(f => f.Barcode == food.Barcode, ct);
            if (existing is not null) return existing;
        }

        var copy = new Food
        {
            Name = food.Name.Length > 200 ? food.Name[..200] : food.Name,
            Brand = food.Brand?.Length > 200 ? food.Brand[..200] : food.Brand,
            Barcode = food.Barcode,
            Calories = food.Calories,
            Protein = food.Protein,
            Carbs = food.Carbs,
            Fat = food.Fat,
            Fiber = food.Fiber,
            DefaultServingGrams = food.DefaultServingGrams > 0 ? food.DefaultServingGrams : 100,
            Source = food.Source
        };

        db.Foods.Add(copy);
        await db.SaveChangesAsync(ct);
        return copy;
    }

    private static void Apply(Food food, NewFood input)
    {
        food.Name = input.Name.Trim();
        food.Brand = string.IsNullOrWhiteSpace(input.Brand) ? null : input.Brand.Trim();
        food.Barcode = string.IsNullOrWhiteSpace(input.Barcode) ? null : input.Barcode.Trim();
        food.Calories = input.Calories;
        food.Protein = input.Protein;
        food.Carbs = input.Carbs;
        food.Fat = input.Fat;
        food.Fiber = input.Fiber;
        food.DefaultServingGrams = input.DefaultServingGrams;
    }

    private static void Validate(NewFood input)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            throw new ArgumentException("Името на храната е задължително.");
        if (new[] { input.Calories, input.Protein, input.Carbs, input.Fat, input.Fiber }.Any(v => v < 0))
            throw new ArgumentException("Нутриентните стойности не може да са отрицателни.");
        if (input.Protein + input.Carbs + input.Fat > 100)
            throw new ArgumentException("Сумата на макросите надхвърля 100 g на 100 g продукт.");
        if (input.DefaultServingGrams <= 0)
            throw new ArgumentException("Порцията трябва да е положително число.");
    }
}
