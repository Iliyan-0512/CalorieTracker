using CalorieTracker.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Core.Data;

public class CalorieDbContext(DbContextOptions<CalorieDbContext> options) : DbContext(options)
{
    public DbSet<Food> Foods => Set<Food>();
    public DbSet<MealEntry> MealEntries => Set<MealEntry>();
    public DbSet<WeightEntry> WeightEntries => Set<WeightEntry>();
    public DbSet<UserProfile> Profiles => Set<UserProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Food>(food =>
        {
            food.Property(f => f.Name).HasMaxLength(200);
            food.Property(f => f.Brand).HasMaxLength(200);
            food.Property(f => f.Barcode).HasMaxLength(32);
            food.HasIndex(f => f.Name);
            food.HasIndex(f => f.Barcode);
            food.Ignore(f => f.Per100g);
            food.HasData(FoodSeed.Foods);
        });

        modelBuilder.Entity<MealEntry>(entry =>
        {
            entry.HasIndex(e => e.Date);
            entry.HasOne(e => e.Food).WithMany().HasForeignKey(e => e.FoodId).OnDelete(DeleteBehavior.Restrict);
            entry.Ignore(e => e.Nutrients);
        });

        modelBuilder.Entity<WeightEntry>().HasIndex(w => w.Date).IsUnique();

        modelBuilder.Entity<UserProfile>().HasData(new UserProfile());
    }
}
