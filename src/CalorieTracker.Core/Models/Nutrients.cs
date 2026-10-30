namespace CalorieTracker.Core.Models;

/// <summary>Калории и макронутриенти. Стойностите за храна са на 100 g.</summary>
public readonly record struct Nutrients(double Calories, double Protein, double Carbs, double Fat, double Fiber)
{
    public static readonly Nutrients Zero = new(0, 0, 0, 0, 0);

    public static Nutrients operator +(Nutrients a, Nutrients b) =>
        new(a.Calories + b.Calories, a.Protein + b.Protein, a.Carbs + b.Carbs, a.Fat + b.Fat, a.Fiber + b.Fiber);

    public static Nutrients operator -(Nutrients a, Nutrients b) =>
        new(a.Calories - b.Calories, a.Protein - b.Protein, a.Carbs - b.Carbs, a.Fat - b.Fat, a.Fiber - b.Fiber);

    public static Nutrients operator *(Nutrients n, double factor) =>
        new(n.Calories * factor, n.Protein * factor, n.Carbs * factor, n.Fat * factor, n.Fiber * factor);

    public Nutrients Rounded(int digits = 1) =>
        new(Math.Round(Calories, digits), Math.Round(Protein, digits), Math.Round(Carbs, digits),
            Math.Round(Fat, digits), Math.Round(Fiber, digits));
}
