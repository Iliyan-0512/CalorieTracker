using CalorieTracker.Core.Models;
using CalorieTracker.Core.Nutrition;

namespace CalorieTracker.Tests;

public class NutritionCalculatorTests
{
    [Fact]
    public void Bmr_UsesMifflinStJeor()
    {
        // 10*80 + 6.25*180 - 5*30 + 5 = 1780
        Assert.Equal(1780, NutritionCalculator.CalculateBmr(Sex.Male, 80, 180, 30));
        // 10*60 + 6.25*165 - 5*25 - 161 = 1345.25
        Assert.Equal(1345.25, NutritionCalculator.CalculateBmr(Sex.Female, 60, 165, 25));
    }

    [Fact]
    public void Targets_ApplyActivityGoalAndMacroSplit()
    {
        var profile = new UserProfile
        {
            Sex = Sex.Male, Age = 30, HeightCm = 180, WeightKg = 80,
            ActivityLevel = ActivityLevel.Moderate, Goal = GoalType.Lose,
            CarbsPercent = 40, ProteinPercent = 30, FatPercent = 30
        };

        var targets = NutritionCalculator.CalculateTargets(profile);

        Assert.Equal(1780, targets.Bmr);
        Assert.Equal(2759, targets.Tdee);                 // 1780 * 1.55
        Assert.Equal(2259, targets.Calories);             // TDEE - 500
        Assert.Equal(Math.Round(2259 * 0.30 / 4), targets.ProteinGrams);
        Assert.Equal(Math.Round(2259 * 0.40 / 4), targets.CarbsGrams);
        Assert.Equal(Math.Round(2259 * 0.30 / 9), targets.FatGrams);
    }

    [Fact]
    public void Targets_NeverGoBelowMinimum()
    {
        var profile = new UserProfile
        {
            Sex = Sex.Female, Age = 70, HeightCm = 150, WeightKg = 45,
            ActivityLevel = ActivityLevel.Sedentary, Goal = GoalType.Lose
        };

        Assert.Equal(NutritionCalculator.MinimumCalories, NutritionCalculator.CalculateTargets(profile).Calories);
    }

    [Fact]
    public void Targets_UseOverrideWhenSet()
    {
        var profile = new UserProfile { CalorieTargetOverride = 2000 };
        Assert.Equal(2000, NutritionCalculator.CalculateTargets(profile).Calories);
    }

    [Fact]
    public void MacroSplit_MustSumTo100()
    {
        Assert.Throws<ArgumentException>(() => NutritionCalculator.ValidateMacroSplit(50, 30, 30));
    }

    [Fact]
    public void Summarize_SumsEntriesPerMealAndComputesRemaining()
    {
        var oats = new Food { Id = 1, Name = "Овес", Calories = 389, Protein = 16.9, Carbs = 66.3, Fat = 6.9, Fiber = 10.6 };
        var egg = new Food { Id = 2, Name = "Яйце", Calories = 143, Protein = 12.6, Carbs = 0.7, Fat = 9.5 };
        var date = new DateOnly(2026, 10, 30);

        var entries = new[]
        {
            new MealEntry { Date = date, MealType = MealType.Breakfast, Food = oats, Grams = 50 },
            new MealEntry { Date = date, MealType = MealType.Breakfast, Food = egg, Grams = 100 },
            new MealEntry { Date = date.AddDays(-1), MealType = MealType.Lunch, Food = egg, Grams = 500 },
        };
        var targets = new DailyTargets(1700, 2600, 2000, 150, 200, 67);

        var summary = NutritionCalculator.Summarize(date, entries, targets);

        Assert.Equal(337.5, summary.Consumed.Calories);   // 194.5 + 143
        Assert.InRange(summary.Consumed.Protein, 21.0, 21.1); // 8.45 + 12.6, закръглено до 0.1
        Assert.Equal(1662.5, summary.Remaining.Calories);
        Assert.Equal(2, summary.Meals.Single(m => m.MealType == MealType.Breakfast).Entries.Count);
        Assert.Empty(summary.Meals.Single(m => m.MealType == MealType.Lunch).Entries);
        Assert.Equal(4, summary.Meals.Count);
    }

    [Fact]
    public void Food_ScalesNutrientsByGrams()
    {
        var food = new Food { Name = "Ориз", Calories = 130, Protein = 2.7, Carbs = 28.2, Fat = 0.3 };
        var n = food.ForGrams(200);
        Assert.Equal(260, n.Calories);
        Assert.Equal(56.4, n.Carbs, 6);
    }
}
