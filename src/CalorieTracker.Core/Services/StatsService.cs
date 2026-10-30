using CalorieTracker.Core.Data;
using CalorieTracker.Core.Models;
using CalorieTracker.Core.Nutrition;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Core.Services;

public record DayStat(DateOnly Date, Nutrients Consumed, double TargetCalories);

public record Stats(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<DayStat> Days,
    IReadOnlyList<WeightEntry> Weights,
    double AverageCalories,
    int DaysLogged,
    int DaysOnTarget,
    double? WeightChangeKg);

public class StatsService(CalorieDbContext db, ProfileService profiles, WeightService weights)
{
    /// <summary>Ден се брои "в целта", ако калориите са в рамките на ±10% от целта.</summary>
    public const double OnTargetTolerance = 0.10;

    public async Task<Stats> GetAsync(DateOnly to, int days, CancellationToken ct = default)
    {
        if (days is < 1 or > 365) throw new ArgumentException("Периодът трябва да е между 1 и 365 дни.");

        var from = to.AddDays(-(days - 1));
        var targets = await profiles.GetTargetsAsync(ct);

        var entries = await db.MealEntries.AsNoTracking()
            .Include(e => e.Food)
            .Where(e => e.Date >= from && e.Date <= to)
            .ToListAsync(ct);

        var byDate = entries.ToLookup(e => e.Date);
        var dayStats = Enumerable.Range(0, days)
            .Select(i => from.AddDays(i))
            .Select(d => new DayStat(d, NutritionCalculator.Sum(byDate[d]).Rounded(), targets.Calories))
            .ToList();

        var logged = dayStats.Where(d => d.Consumed.Calories > 0).ToList();
        var onTarget = logged.Count(d => Math.Abs(d.Consumed.Calories - d.TargetCalories) <= d.TargetCalories * OnTargetTolerance);

        var weightHistory = await weights.GetHistoryAsync(from, to, ct);
        double? weightChange = weightHistory.Count >= 2
            ? Math.Round(weightHistory[^1].WeightKg - weightHistory[0].WeightKg, 1)
            : null;

        return new Stats(
            from,
            to,
            dayStats,
            weightHistory,
            logged.Count == 0 ? 0 : Math.Round(logged.Average(d => d.Consumed.Calories)),
            logged.Count,
            onTarget,
            weightChange);
    }
}
