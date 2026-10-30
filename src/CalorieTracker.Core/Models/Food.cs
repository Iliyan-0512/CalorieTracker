namespace CalorieTracker.Core.Models;

public class Food
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Brand { get; set; }
    public string? Barcode { get; set; }

    // Стойности на 100 g
    public double Calories { get; set; }
    public double Protein { get; set; }
    public double Carbs { get; set; }
    public double Fat { get; set; }
    public double Fiber { get; set; }

    /// <summary>Типична порция в грамове (за бързо добавяне).</summary>
    public double DefaultServingGrams { get; set; } = 100;

    public FoodSource Source { get; set; } = FoodSource.Builtin;

    public Nutrients Per100g => new(Calories, Protein, Carbs, Fat, Fiber);

    public Nutrients ForGrams(double grams) => Per100g * (grams / 100.0);
}

public enum FoodSource
{
    Builtin,
    Custom,
    OpenFoodFacts
}
