using CalorieTracker.Core.Data;
using CalorieTracker.Core.Models;
using CalorieTracker.Core.Nutrition;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Core.Services;

public class ProfileService(CalorieDbContext db)
{
    public async Task<UserProfile> GetAsync(CancellationToken ct = default)
    {
        var profile = await db.Profiles.AsNoTracking().FirstOrDefaultAsync(ct);
        if (profile is not null) return profile;

        profile = new UserProfile();
        db.Profiles.Add(profile);
        await db.SaveChangesAsync(ct);
        return profile;
    }

    public async Task<DailyTargets> GetTargetsAsync(CancellationToken ct = default) =>
        NutritionCalculator.CalculateTargets(await GetAsync(ct));

    public async Task<UserProfile> SaveAsync(UserProfile input, CancellationToken ct = default)
    {
        Validate(input);

        var profile = await db.Profiles.FirstOrDefaultAsync(ct);
        if (profile is null)
        {
            profile = new UserProfile();
            db.Profiles.Add(profile);
        }

        profile.Sex = input.Sex;
        profile.Age = input.Age;
        profile.HeightCm = input.HeightCm;
        profile.WeightKg = input.WeightKg;
        profile.ActivityLevel = input.ActivityLevel;
        profile.Goal = input.Goal;
        profile.CalorieTargetOverride = input.CalorieTargetOverride;
        profile.CarbsPercent = input.CarbsPercent;
        profile.ProteinPercent = input.ProteinPercent;
        profile.FatPercent = input.FatPercent;

        await db.SaveChangesAsync(ct);
        return profile;
    }

    private static void Validate(UserProfile p)
    {
        if (p.Age is < 13 or > 120) throw new ArgumentException("Възрастта трябва да е между 13 и 120 години.");
        if (p.HeightCm is < 100 or > 250) throw new ArgumentException("Ръстът трябва да е между 100 и 250 cm.");
        if (p.WeightKg is < 30 or > 400) throw new ArgumentException("Теглото трябва да е между 30 и 400 kg.");
        if (p.CalorieTargetOverride is < NutritionCalculator.MinimumCalories or > 10000)
            throw new ArgumentException($"Ръчната цел за калории трябва да е между {NutritionCalculator.MinimumCalories} и 10000.");
        NutritionCalculator.ValidateMacroSplit(p.CarbsPercent, p.ProteinPercent, p.FatPercent);
    }
}
