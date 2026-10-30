using CalorieTracker.Core.Data;
using CalorieTracker.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Core.Services;

public class WeightService(CalorieDbContext db)
{
    public async Task<IReadOnlyList<WeightEntry>> GetHistoryAsync(DateOnly from, DateOnly to, CancellationToken ct = default) =>
        await db.WeightEntries.AsNoTracking()
            .Where(w => w.Date >= from && w.Date <= to)
            .OrderBy(w => w.Date)
            .ToListAsync(ct);

    /// <summary>Записва теглото за деня (един запис на ден). Последното тегло обновява и профила, за да се преизчисли TDEE.</summary>
    public async Task<WeightEntry> LogAsync(DateOnly date, double weightKg, CancellationToken ct = default)
    {
        if (weightKg is < 30 or > 400) throw new ArgumentException("Теглото трябва да е между 30 и 400 kg.");

        var entry = await db.WeightEntries.FirstOrDefaultAsync(w => w.Date == date, ct);
        if (entry is null)
        {
            entry = new WeightEntry { Date = date, WeightKg = weightKg };
            db.WeightEntries.Add(entry);
        }
        else
        {
            entry.WeightKg = weightKg;
        }

        var latestDate = await db.WeightEntries.MaxAsync(w => (DateOnly?)w.Date, ct);
        if (latestDate is null || date >= latestDate)
        {
            var profile = await db.Profiles.FirstOrDefaultAsync(ct);
            if (profile is not null) profile.WeightKg = weightKg;
        }

        await db.SaveChangesAsync(ct);
        return entry;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entry = await db.WeightEntries.FindAsync([id], ct);
        if (entry is null) return false;

        db.WeightEntries.Remove(entry);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
