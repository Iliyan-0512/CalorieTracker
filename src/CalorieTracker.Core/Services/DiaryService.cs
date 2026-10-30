using CalorieTracker.Core.Data;
using CalorieTracker.Core.Models;
using CalorieTracker.Core.Nutrition;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Core.Services;

public class DiaryService(CalorieDbContext db, ProfileService profiles)
{
    public async Task<DailySummary> GetDayAsync(DateOnly date, CancellationToken ct = default)
    {
        var entries = await db.MealEntries.AsNoTracking()
            .Include(e => e.Food)
            .Where(e => e.Date == date)
            .ToListAsync(ct);

        var targets = await profiles.GetTargetsAsync(ct);
        return NutritionCalculator.Summarize(date, entries, targets);
    }

    public async Task<MealEntry> AddEntryAsync(DateOnly date, MealType mealType, int foodId, double grams, CancellationToken ct = default)
    {
        if (grams is <= 0 or > 5000) throw new ArgumentException("Количеството трябва да е между 0 и 5000 g.");

        var food = await db.Foods.FindAsync([foodId], ct)
                   ?? throw new KeyNotFoundException($"Храна с id {foodId} не съществува.");

        var entry = new MealEntry { Date = date, MealType = mealType, FoodId = food.Id, Food = food, Grams = grams };
        db.MealEntries.Add(entry);
        await db.SaveChangesAsync(ct);
        return entry;
    }

    public async Task<bool> UpdateGramsAsync(int entryId, double grams, CancellationToken ct = default)
    {
        if (grams is <= 0 or > 5000) throw new ArgumentException("Количеството трябва да е между 0 и 5000 g.");

        var entry = await db.MealEntries.FindAsync([entryId], ct);
        if (entry is null) return false;

        entry.Grams = grams;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteEntryAsync(int entryId, CancellationToken ct = default)
    {
        var entry = await db.MealEntries.FindAsync([entryId], ct);
        if (entry is null) return false;

        db.MealEntries.Remove(entry);
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Последно използвани храни – за бързо добавяне.</summary>
    public async Task<IReadOnlyList<Food>> GetRecentFoodsAsync(int limit = 10, CancellationToken ct = default)
    {
        var recent = await db.MealEntries.AsNoTracking()
            .Include(e => e.Food)
            .OrderByDescending(e => e.LoggedAt)
            .Take(limit * 5)
            .ToListAsync(ct);

        return recent.Select(e => e.Food!).DistinctBy(f => f.Id).Take(limit).ToList();
    }
}
