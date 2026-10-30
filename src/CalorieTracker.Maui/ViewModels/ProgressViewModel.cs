using CalorieTracker.Core.Models;
using CalorieTracker.Core.Services;
using CalorieTracker.Maui.Charts;
using CalorieTracker.Maui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CalorieTracker.Maui.ViewModels;

public partial class ProgressViewModel(CoreServices core) : ObservableObject
{
    private static readonly int[] PeriodDays = [7, 30, 90];

    public IReadOnlyList<string> Periods => Labels.Periods;

    public CaloriesChartDrawable CaloriesChart { get; } = new();
    public WeightChartDrawable WeightChart { get; } = new();

    /// <summary>Страницата прерисува графиките, когато данните се сменят.</summary>
    public event EventHandler? ChartsChanged;

    [ObservableProperty] private int _periodIndex;
    [ObservableProperty] private string _weightInput = "";
    [ObservableProperty] private string _averageText = "–";
    [ObservableProperty] private string _daysLoggedText = "–";
    [ObservableProperty] private string _onTargetText = "–";
    [ObservableProperty] private string _weightChangeText = "–";
    [ObservableProperty] private bool _hasWeights;

    partial void OnPeriodIndexChanged(int value) => LoadCommand.Execute(null);

    [RelayCommand]
    private async Task LoadAsync()
    {
        var days = PeriodDays[Math.Clamp(PeriodIndex, 0, PeriodDays.Length - 1)];
        var stats = await core.RunAsync<StatsService, Stats>(s => s.GetAsync(DateOnly.FromDateTime(DateTime.Today), days));

        AverageText = stats.DaysLogged > 0 ? Labels.Kcal(stats.AverageCalories) : "–";
        DaysLoggedText = $"{stats.DaysLogged} от {stats.Days.Count}";
        OnTargetText = stats.DaysLogged > 0 ? $"{stats.DaysOnTarget} дни" : "–";
        WeightChangeText = stats.WeightChangeKg is { } change ? $"{change:+0.0;-0.0;0.0} kg" : "–";
        HasWeights = stats.Weights.Count > 0;

        CaloriesChart.Days = stats.Days;
        WeightChart.Weights = stats.Weights;
        ChartsChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task LogWeightAsync()
    {
        if (!Labels.TryParseNumber(WeightInput, out var kg))
        {
            await Shell.Current.DisplayAlertAsync("Грешка", "Въведете тегло в kg.", "OK");
            return;
        }

        try
        {
            await core.RunAsync<WeightService, WeightEntry>(s => s.LogAsync(DateOnly.FromDateTime(DateTime.Today), kg));
            WeightInput = "";
            await LoadAsync();
        }
        catch (ArgumentException ex)
        {
            await Shell.Current.DisplayAlertAsync("Грешка", ex.Message, "OK");
        }
    }
}
