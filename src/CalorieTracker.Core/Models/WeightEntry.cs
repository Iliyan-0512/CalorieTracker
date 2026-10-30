namespace CalorieTracker.Core.Models;

public class WeightEntry
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public double WeightKg { get; set; }
}
