using CalorieTracker.Core.Models;

namespace CalorieTracker.Core.Nutrition;

public record DailyTargets(double Bmr, double Tdee, double Calories, double ProteinGrams, double CarbsGrams, double FatGrams);

public record MealGroup(MealType MealType, IReadOnlyList<MealEntry> Entries, Nutrients Totals);

public record DailySummary(DateOnly Date, Nutrients Consumed, DailyTargets Targets, Nutrients Remaining, IReadOnlyList<MealGroup> Meals);

public static class NutritionCalculator
{
    public const double KcalPerGramProtein = 4;
    public const double KcalPerGramCarbs = 4;
    public const double KcalPerGramFat = 9;

    /// <summary>Под тази стойност не препоръчваме дневен прием без лекарски контрол.</summary>
    public const double MinimumCalories = 1200;

    public const double LoseAdjustment = -500;
    public const double GainAdjustment = 300;

    /// <summary>Базов метаболизъм по формулата на Mifflin-St Jeor.</summary>
    public static double CalculateBmr(Sex sex, double weightKg, double heightCm, int age)
    {
        var bmr = 10 * weightKg + 6.25 * heightCm - 5 * age;
        return sex == Sex.Male ? bmr + 5 : bmr - 161;
    }

    public static double ActivityFactor(ActivityLevel level) => level switch
    {
        ActivityLevel.Sedentary => 1.2,
        ActivityLevel.Light => 1.375,
        ActivityLevel.Moderate => 1.55,
        ActivityLevel.Active => 1.725,
        ActivityLevel.VeryActive => 1.9,
        _ => throw new ArgumentOutOfRangeException(nameof(level))
    };

    public static double GoalAdjustment(GoalType goal) => goal switch
    {
        GoalType.Lose => LoseAdjustment,
        GoalType.Gain => GainAdjustment,
        _ => 0
    };

    public static DailyTargets CalculateTargets(UserProfile profile)
    {
        ValidateMacroSplit(profile.CarbsPercent, profile.ProteinPercent, profile.FatPercent);

        var bmr = CalculateBmr(profile.Sex, profile.WeightKg, profile.HeightCm, profile.Age);
        var tdee = bmr * ActivityFactor(profile.ActivityLevel);
        var calories = profile.CalorieTargetOverride
            ?? Math.Max(MinimumCalories, tdee + GoalAdjustment(profile.Goal));

        return new DailyTargets(
            Math.Round(bmr),
            Math.Round(tdee),
            Math.Round(calories),
            Math.Round(calories * profile.ProteinPercent / 100.0 / KcalPerGramProtein),
            Math.Round(calories * profile.CarbsPercent / 100.0 / KcalPerGramCarbs),
            Math.Round(calories * profile.FatPercent / 100.0 / KcalPerGramFat));
    }

    public static void ValidateMacroSplit(int carbsPercent, int proteinPercent, int fatPercent)
    {
        if (carbsPercent < 0 || proteinPercent < 0 || fatPercent < 0)
            throw new ArgumentException("Процентите за макроси не може да са отрицателни.");
        if (carbsPercent + proteinPercent + fatPercent != 100)
            throw new ArgumentException("Сумата от процентите за въглехидрати, белтъчини и мазнини трябва да е 100.");
    }

    public static DailySummary Summarize(DateOnly date, IEnumerable<MealEntry> entries, DailyTargets targets)
    {
        var list = entries.Where(e => e.Date == date).ToList();

        var meals = Enum.GetValues<MealType>()
            .Select(type =>
            {
                var items = list.Where(e => e.MealType == type).OrderBy(e => e.LoggedAt).ToList();
                return new MealGroup(type, items, Sum(items).Rounded());
            })
            .ToList();

        var consumed = Sum(list);
        // Няма цел за фибри, затова остатъкът за тях винаги е 0
        var targetNutrients = new Nutrients(targets.Calories, targets.ProteinGrams, targets.CarbsGrams, targets.FatGrams, consumed.Fiber);
        var remaining = targetNutrients - consumed;

        return new DailySummary(date, consumed.Rounded(), targets, remaining.Rounded(), meals);
    }

    public static Nutrients Sum(IEnumerable<MealEntry> entries) =>
        entries.Aggregate(Nutrients.Zero, (acc, e) => acc + e.Nutrients);
}
