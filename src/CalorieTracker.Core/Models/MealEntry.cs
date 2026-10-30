namespace CalorieTracker.Core.Models;

public enum MealType
{
    Breakfast,
    Lunch,
    Dinner,
    Snack
}

public class MealEntry
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public MealType MealType { get; set; }
    public int FoodId { get; set; }
    public Food? Food { get; set; }
    public double Grams { get; set; }
    public DateTime LoggedAt { get; set; } = DateTime.UtcNow;

    public Nutrients Nutrients => Food?.ForGrams(Grams) ?? Nutrients.Zero;
}
