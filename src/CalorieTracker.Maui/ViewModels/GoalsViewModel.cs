using System.ComponentModel;
using CalorieTracker.Core.Models;
using CalorieTracker.Core.Nutrition;
using CalorieTracker.Core.Services;
using CalorieTracker.Maui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CalorieTracker.Maui.ViewModels;

public partial class GoalsViewModel : ObservableObject
{
    private readonly CoreServices _core;
    private bool _loading;

    public IReadOnlyList<string> Sexes => Labels.Sexes;
    public IReadOnlyList<string> ActivityLevels => Labels.ActivityLevels;
    public IReadOnlyList<string> Goals => Labels.Goals;
    public IReadOnlyList<string> Themes => Labels.Themes;

    [ObservableProperty] private int _sexIndex;
    [ObservableProperty] private int _activityIndex;
    [ObservableProperty] private int _goalIndex;
    [ObservableProperty] private string _age = "";
    [ObservableProperty] private string _height = "";
    [ObservableProperty] private string _weight = "";
    [ObservableProperty] private string _calorieOverride = "";
    [ObservableProperty] private string _carbsPercent = "";
    [ObservableProperty] private string _proteinPercent = "";
    [ObservableProperty] private string _fatPercent = "";
    [ObservableProperty] private int _themeIndex = ThemeService.Current;

    [ObservableProperty] private string _bmrText = "–";
    [ObservableProperty] private string _tdeeText = "–";
    [ObservableProperty] private string _targetText = "–";
    [ObservableProperty] private string _macrosText = "–";
    [ObservableProperty] private string _validationText = "";

    public GoalsViewModel(CoreServices core)
    {
        _core = core;
        PropertyChanged += OnAnyPropertyChanged;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        var p = await _core.RunAsync<ProfileService, UserProfile>(s => s.GetAsync());

        _loading = true;
        SexIndex = (int)p.Sex;
        ActivityIndex = (int)p.ActivityLevel;
        GoalIndex = (int)p.Goal;
        Age = p.Age.ToString();
        Height = Labels.Number(p.HeightCm);
        Weight = Labels.Number(p.WeightKg);
        CalorieOverride = p.CalorieTargetOverride is { } o ? Labels.Number(o) : "";
        CarbsPercent = p.CarbsPercent.ToString();
        ProteinPercent = p.ProteinPercent.ToString();
        FatPercent = p.FatPercent.ToString();
        _loading = false;

        Recalculate();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (BuildProfile() is not { } profile)
        {
            await Shell.Current.DisplayAlertAsync("Грешка", ValidationText, "OK");
            return;
        }

        try
        {
            await _core.RunAsync<ProfileService, UserProfile>(s => s.SaveAsync(profile));
            await Shell.Current.DisplayAlertAsync("Запазено", "Целите са обновени.", "OK");
        }
        catch (ArgumentException ex)
        {
            await Shell.Current.DisplayAlertAsync("Грешка", ex.Message, "OK");
        }
    }

    partial void OnThemeIndexChanged(int value) => ThemeService.Current = value;

    private void OnAnyPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Изчисленията се обновяват веднага при промяна на полетата
        if (_loading || e.PropertyName is nameof(BmrText) or nameof(TdeeText) or nameof(TargetText)
                or nameof(MacrosText) or nameof(ValidationText) or nameof(ThemeIndex))
            return;
        Recalculate();
    }

    private void Recalculate()
    {
        if (BuildProfile() is not { } profile)
        {
            BmrText = TdeeText = TargetText = MacrosText = "–";
            return;
        }

        var t = NutritionCalculator.CalculateTargets(profile);
        BmrText = Labels.Kcal(t.Bmr);
        TdeeText = Labels.Kcal(t.Tdee);
        TargetText = Labels.Kcal(t.Calories);
        MacrosText = $"Б {t.ProteinGrams:0} g · В {t.CarbsGrams:0} g · М {t.FatGrams:0} g";
    }

    private UserProfile? BuildProfile()
    {
        ValidationText = "";

        if (!int.TryParse(Age, out var age) || !Labels.TryParseNumber(Height, out var height) ||
            !Labels.TryParseNumber(Weight, out var weight))
        {
            ValidationText = "Попълнете възраст, ръст и тегло.";
            return null;
        }

        if (!int.TryParse(CarbsPercent, out var carbs) || !int.TryParse(ProteinPercent, out var protein) ||
            !int.TryParse(FatPercent, out var fat))
        {
            ValidationText = "Попълнете процентите за макросите.";
            return null;
        }

        if (carbs + protein + fat != 100)
        {
            ValidationText = $"Сумата на макросите е {carbs + protein + fat}%, а трябва да е 100%.";
            return null;
        }

        double? calorieOverride = null;
        if (!string.IsNullOrWhiteSpace(CalorieOverride))
        {
            if (!Labels.TryParseNumber(CalorieOverride, out var o) || o < NutritionCalculator.MinimumCalories)
            {
                ValidationText = $"Ръчната цел трябва да е поне {NutritionCalculator.MinimumCalories} kcal.";
                return null;
            }
            calorieOverride = o;
        }

        return new UserProfile
        {
            Sex = (Sex)Math.Max(0, SexIndex),
            Age = age,
            HeightCm = height,
            WeightKg = weight,
            ActivityLevel = (ActivityLevel)Math.Max(0, ActivityIndex),
            Goal = (GoalType)Math.Max(0, GoalIndex),
            CalorieTargetOverride = calorieOverride,
            CarbsPercent = carbs,
            ProteinPercent = protein,
            FatPercent = fat
        };
    }
}
