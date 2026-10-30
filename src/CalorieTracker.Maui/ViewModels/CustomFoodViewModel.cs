using CalorieTracker.Core.Models;
using CalorieTracker.Core.Services;
using CalorieTracker.Maui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CalorieTracker.Maui.ViewModels;

public partial class CustomFoodViewModel(CoreServices core) : ObservableObject, IQueryAttributable
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _brand = "";
    [ObservableProperty] private string _barcode = "";
    [ObservableProperty] private string _calories = "";
    [ObservableProperty] private string _protein = "";
    [ObservableProperty] private string _carbs = "";
    [ObservableProperty] private string _fat = "";
    [ObservableProperty] private string _fiber = "0";
    [ObservableProperty] private string _serving = "100";

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("barcode", out var code) && code is string barcode) Barcode = barcode;
        query.Clear();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!TryRead(out var input, out var error))
        {
            await Shell.Current.DisplayAlertAsync("Грешка", error, "OK");
            return;
        }

        try
        {
            var food = await core.RunAsync<FoodService, Food>(s => s.AddCustomAsync(input));
            await Shell.Current.GoToAsync("..", new Dictionary<string, object> { ["foodId"] = food.Id });
        }
        catch (ArgumentException ex)
        {
            await Shell.Current.DisplayAlertAsync("Грешка", ex.Message, "OK");
        }
    }

    private bool TryRead(out NewFood input, out string error)
    {
        input = null!;
        error = "";

        if (string.IsNullOrWhiteSpace(Name))
        {
            error = "Въведете име на храната.";
            return false;
        }

        if (!Labels.TryParseNumber(Calories, out var kcal) || !Labels.TryParseNumber(Protein, out var protein) ||
            !Labels.TryParseNumber(Carbs, out var carbs) || !Labels.TryParseNumber(Fat, out var fat))
        {
            error = "Попълнете калории, белтъчини, въглехидрати и мазнини (на 100 g).";
            return false;
        }

        var fiber = Labels.TryParseNumber(Fiber, out var f) ? f : 0;
        var serving = Labels.TryParseNumber(Serving, out var s) && s > 0 ? s : 100;

        input = new NewFood(Name, Brand, Barcode, kcal, protein, carbs, fat, fiber, serving);
        return true;
    }
}
