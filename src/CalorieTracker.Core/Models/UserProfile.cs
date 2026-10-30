namespace CalorieTracker.Core.Models;

public enum Sex
{
    Male,
    Female
}

public enum ActivityLevel
{
    Sedentary,
    Light,
    Moderate,
    Active,
    VeryActive
}

public enum GoalType
{
    Lose,
    Maintain,
    Gain
}

/// <summary>Профил и цели. Приложението е за лична употреба, затова има един профил (Id = 1).</summary>
public class UserProfile
{
    public int Id { get; set; } = 1;
    public Sex Sex { get; set; } = Sex.Male;
    public int Age { get; set; } = 30;
    public double HeightCm { get; set; } = 175;
    public double WeightKg { get; set; } = 75;
    public ActivityLevel ActivityLevel { get; set; } = ActivityLevel.Moderate;
    public GoalType Goal { get; set; } = GoalType.Maintain;

    /// <summary>Ако е зададено, заменя автоматично изчислената цел за калории.</summary>
    public double? CalorieTargetOverride { get; set; }

    // Разпределение на макросите в проценти от калориите (сумата трябва да е 100)
    public int CarbsPercent { get; set; } = 50;
    public int ProteinPercent { get; set; } = 30;
    public int FatPercent { get; set; } = 20;
}
