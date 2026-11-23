using System.Collections.ObjectModel;
using CalorieTracker.Core.Models;
using CalorieTracker.Core.Services;
using CalorieTracker.Maui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CalorieTracker.Maui.ViewModels;

public partial class AddFoodViewModel(CoreServices core) : ObservableObject, IQueryAttributable
{
    private DateOnly _date = DateOnly.FromDateTime(DateTime.Today);
    private CancellationTokenSource? _searchCts;

    public IReadOnlyList<string> MealTypes => Labels.MealTypes;

    [ObservableProperty] private int _mealIndex;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _barcode = "";
    [ObservableProperty] private string _listHeader = "Последно използвани";
    [ObservableProperty] private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFoodSelected), nameof(CanEditSelected))]
    private Food? _selectedFood;

    [ObservableProperty] private string _gramsText = "100";
    [ObservableProperty] private string _previewText = "";

    public bool IsFoodSelected => SelectedFood is not null;

    /// <summary>Онлайн резултатите се редактират след като се запишат локално.</summary>
    public bool CanEditSelected => SelectedFood is { Id: > 0 };

    public ObservableCollection<Food> Results { get; } = [];

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("date", out var date) && date is DateOnly d) _date = d;
        if (query.TryGetValue("meal", out var meal) && meal is MealType m) MealIndex = (int)m;

        // Връщане от екрана "Нова храна" / "Редакция"
        if (query.TryGetValue("foodId", out var id) && id is int foodId)
            _ = SelectByIdAsync(foodId);

        // Връщане от скенера
        if (query.TryGetValue("scannedBarcode", out var code) && code is string barcode)
        {
            Barcode = barcode;
            _ = LookupBarcodeAsync();
        }

        query.Clear();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (Results.Count == 0 && string.IsNullOrWhiteSpace(SearchText))
            await RunSearchAsync("", CancellationToken.None);
    }

    partial void OnSearchTextChanged(string value)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        // Малко изчакване, за да не търсим при всеки натиснат клавиш
        _ = Task.Delay(250, token).ContinueWith(
            async _ => await MainThread.InvokeOnMainThreadAsync(() => RunSearchAsync(value, token)),
            token, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
    }

    partial void OnSelectedFoodChanged(Food? value)
    {
        if (value is null) return;
        GramsText = Labels.Number(value.DefaultServingGrams);
        UpdatePreview();
    }

    partial void OnGramsTextChanged(string value) => UpdatePreview();

    [RelayCommand]
    private void Select(Food food) => SelectedFood = food;

    [RelayCommand]
    private void ClearSelection() => SelectedFood = null;

    [RelayCommand]
    private async Task SearchOnlineAsync()
    {
        var query = SearchText.Trim();
        if (query.Length < 2)
        {
            await Shell.Current.DisplayAlertAsync("Търсене", "Въведете поне 2 букви в полето за търсене.", "OK");
            return;
        }

        _searchCts?.Cancel();
        IsBusy = true;
        try
        {
            var foods = await core.RunAsync<FoodService, IReadOnlyList<Food>>(s => s.SearchOnlineAsync(query));
            Results.Clear();
            foreach (var food in foods) Results.Add(food);
            ListHeader = foods.Count > 0
                ? $"Онлайн: {foods.Count} резултата (OpenFoodFacts, USDA)"
                : "Няма онлайн резултати (или няма интернет) – опитайте AI";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task ScanAsync() => Shell.Current.GoToAsync(nameof(Views.ScanBarcodePage));

    [RelayCommand]
    private Task AiAsync() =>
        Shell.Current.GoToAsync(nameof(Views.AiFoodPage), new Dictionary<string, object>
        {
            ["date"] = _date,
            ["meal"] = MealIndex,
            ["text"] = SearchText.Trim()
        });

    [RelayCommand]
    private async Task LookupBarcodeAsync()
    {
        var code = Barcode.Trim();
        if (code.Length == 0) return;

        IsBusy = true;
        try
        {
            var food = await core.RunAsync<FoodService, Food?>(s => s.FindByBarcodeAsync(code));
            if (food is not null)
            {
                SelectedFood = food;
                return;
            }

            var create = await Shell.Current.DisplayAlertAsync("Не е намерено",
                $"Продукт с баркод {code} не е намерен (или няма интернет). Да го добавите ли ръчно?", "Добави", "Отказ");
            if (create) await NewFoodAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task NewFoodAsync() =>
        Shell.Current.GoToAsync(nameof(Views.CustomFoodPage), new Dictionary<string, object> { ["barcode"] = Barcode.Trim() });

    [RelayCommand]
    private Task EditSelectedAsync() =>
        SelectedFood is { Id: > 0 } food
            ? Shell.Current.GoToAsync(nameof(Views.CustomFoodPage), new Dictionary<string, object> { ["editFoodId"] = food.Id })
            : Task.CompletedTask;

    [RelayCommand]
    private async Task AddAsync()
    {
        if (SelectedFood is not { } food) return;

        if (!Labels.TryParseNumber(GramsText, out var grams) || grams <= 0)
        {
            await Shell.Current.DisplayAlertAsync("Грешка", "Въведете количество в грамове.", "OK");
            return;
        }

        // Храна от онлайн търсене първо се записва локално
        if (food.Id == 0)
            food = await core.RunAsync<FoodService, Food>(s => s.SaveExternalAsync(food));

        await core.RunAsync<DiaryService, MealEntry>(s => s.AddEntryAsync(_date, (MealType)MealIndex, food.Id, grams));
        await Shell.Current.GoToAsync("..");
    }

    private async Task SelectByIdAsync(int id)
    {
        SelectedFood = null; // за да се опреснят стойностите след редакция
        SelectedFood = await core.RunAsync<FoodService, Food?>(s => s.GetAsync(id));
        await RunSearchAsync(SearchText, CancellationToken.None);
    }

    private async Task RunSearchAsync(string query, CancellationToken token)
    {
        IReadOnlyList<Food> foods;
        if (string.IsNullOrWhiteSpace(query))
        {
            foods = await core.RunAsync<DiaryService, IReadOnlyList<Food>>(s => s.GetRecentFoodsAsync());
            ListHeader = foods.Count > 0 ? "Последно използвани" : "Всички храни";
            if (foods.Count == 0)
                foods = await core.RunAsync<FoodService, IReadOnlyList<Food>>(s => s.SearchAsync(null));
        }
        else
        {
            foods = await core.RunAsync<FoodService, IReadOnlyList<Food>>(s => s.SearchAsync(query));
            ListHeader = foods.Count > 0
                ? "В моята база – не е това? Натиснете „Онлайн“ или „AI“"
                : "Няма в моята база – натиснете „Онлайн“ или „AI“";
        }

        if (token.IsCancellationRequested) return;

        Results.Clear();
        foreach (var food in foods) Results.Add(food);
    }

    private void UpdatePreview()
    {
        if (SelectedFood is not { } food || !Labels.TryParseNumber(GramsText, out var grams) || grams <= 0)
        {
            PreviewText = "";
            return;
        }

        var n = food.ForGrams(grams);
        PreviewText = $"{n.Calories:0} kcal · Б {n.Protein:0.#} g · В {n.Carbs:0.#} g · М {n.Fat:0.#} g · Ф {n.Fiber:0.#} g";
    }
}
