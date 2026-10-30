using System.Collections.ObjectModel;
using CalorieTracker.Core.Models;
using CalorieTracker.Core.Nutrition;
using CalorieTracker.Core.Services;
using CalorieTracker.Maui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CalorieTracker.Maui.ViewModels;

public record MacroProgress(string Name, string Text, double Progress, bool IsOver);

public record EntryItem(int Id, string Name, string Details, string Kcal, double Grams);

public record MealSection(MealType MealType, string Title, string Kcal, IReadOnlyList<EntryItem> Entries)
{
    public bool IsEmpty => Entries.Count == 0;
}

public partial class DiaryViewModel(CoreServices core) : ObservableObject
{
    private DateOnly _date = DateOnly.FromDateTime(DateTime.Today);

    [ObservableProperty] private string _dateTitle = "Днес";
    [ObservableProperty] private string _consumedText = "0";
    [ObservableProperty] private string _targetText = "0";
    [ObservableProperty] private string _remainingText = "0";
    [ObservableProperty] private string _remainingLabel = "остават";
    [ObservableProperty] private double _calorieProgress;
    [ObservableProperty] private bool _isOverTarget;
    [ObservableProperty] private bool _isBusy;

    public ObservableCollection<MacroProgress> Macros { get; } = [];
    public ObservableCollection<MealSection> Meals { get; } = [];

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var summary = await core.RunAsync<DiaryService, DailySummary>(s => s.GetDayAsync(_date));
            Apply(summary);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task PreviousDayAsync() => ChangeDayAsync(_date.AddDays(-1));

    [RelayCommand]
    private Task NextDayAsync() => ChangeDayAsync(_date.AddDays(1));

    [RelayCommand]
    private Task TodayAsync() => ChangeDayAsync(DateOnly.FromDateTime(DateTime.Today));

    [RelayCommand]
    private Task AddFoodAsync(MealType mealType) =>
        Shell.Current.GoToAsync(nameof(Views.AddFoodPage), new Dictionary<string, object>
        {
            ["date"] = _date,
            ["meal"] = mealType
        });

    [RelayCommand]
    private async Task QuickAddAsync()
    {
        // Бутонът "+" предлага най-вероятното хранене според часа
        var hour = DateTime.Now.Hour;
        var meal = hour switch
        {
            < 11 => MealType.Breakfast,
            < 16 => MealType.Lunch,
            < 21 => MealType.Dinner,
            _ => MealType.Snack
        };
        await AddFoodAsync(meal);
    }

    [RelayCommand]
    private async Task EditEntryAsync(EntryItem entry)
    {
        var input = await Shell.Current.DisplayPromptAsync(entry.Name, "Количество (g):",
            accept: "Запази", cancel: "Отказ", initialValue: Labels.Number(entry.Grams), keyboard: Keyboard.Numeric);
        if (input is null) return;

        if (!Labels.TryParseNumber(input, out var grams) || grams <= 0)
        {
            await Shell.Current.DisplayAlertAsync("Грешка", "Въведете положително число.", "OK");
            return;
        }

        await core.RunAsync<DiaryService>(s => s.UpdateGramsAsync(entry.Id, grams));
        await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteEntryAsync(EntryItem entry)
    {
        await core.RunAsync<DiaryService>(s => s.DeleteEntryAsync(entry.Id));
        await LoadAsync();
    }

    private async Task ChangeDayAsync(DateOnly date)
    {
        _date = date;
        DateTitle = Labels.DayTitle(date);
        await LoadAsync();
    }

    private void Apply(DailySummary summary)
    {
        var t = summary.Targets;
        var c = summary.Consumed;

        DateTitle = Labels.DayTitle(summary.Date);
        ConsumedText = $"{c.Calories:0}";
        TargetText = $"{t.Calories:0}";
        IsOverTarget = summary.Remaining.Calories < 0;
        RemainingText = $"{Math.Abs(summary.Remaining.Calories):0}";
        RemainingLabel = IsOverTarget ? "над целта" : "остават";
        CalorieProgress = t.Calories > 0 ? Math.Min(1, c.Calories / t.Calories) : 0;

        Macros.Clear();
        Macros.Add(Macro("Белтъчини", c.Protein, t.ProteinGrams));
        Macros.Add(Macro("Въглехидрати", c.Carbs, t.CarbsGrams));
        Macros.Add(Macro("Мазнини", c.Fat, t.FatGrams));
        Macros.Add(new MacroProgress("Фибри", Labels.Grams(c.Fiber), 0, false));

        Meals.Clear();
        foreach (var group in summary.Meals)
        {
            var entries = group.Entries
                .Select(e =>
                {
                    var n = e.Nutrients;
                    return new EntryItem(e.Id, e.Food?.Name ?? "?",
                        $"{Labels.Grams(e.Grams)} · Б {n.Protein:0.#} · В {n.Carbs:0.#} · М {n.Fat:0.#}",
                        Labels.Kcal(n.Calories), e.Grams);
                })
                .ToList();
            Meals.Add(new MealSection(group.MealType, Labels.MealName(group.MealType), Labels.Kcal(group.Totals.Calories), entries));
        }
    }

    private static MacroProgress Macro(string name, double consumed, double target) =>
        new(name, $"{consumed:0} / {target:0} g", target > 0 ? Math.Min(1, consumed / target) : 0, consumed > target);
}
