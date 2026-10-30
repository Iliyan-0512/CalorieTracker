using CalorieTracker.Core.Data;
using CalorieTracker.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Core.Services;

public record NewFood(string Name, string? Brand, string? Barcode, double Calories, double Protein, double Carbs,
    double Fat, double Fiber, double DefaultServingGrams = 100);

public class FoodService(CalorieDbContext db, IBarcodeLookup? barcodeLookup = null)
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

        var food = new Food
        {
            Name = input.Name.Trim(),
            Brand = string.IsNullOrWhiteSpace(input.Brand) ? null : input.Brand.Trim(),
            Barcode = string.IsNullOrWhiteSpace(input.Barcode) ? null : input.Barcode.Trim(),
            Calories = input.Calories,
            Protein = input.Protein,
            Carbs = input.Carbs,
            Fat = input.Fat,
            Fiber = input.Fiber,
            DefaultServingGrams = input.DefaultServingGrams,
            Source = FoodSource.Custom
        };

        db.Foods.Add(food);
        await db.SaveChangesAsync(ct);
        return food;
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
