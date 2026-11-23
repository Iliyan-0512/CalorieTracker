using System.Collections.ObjectModel;
using CalorieTracker.Core.Models;
using CalorieTracker.Core.Services;
using CalorieTracker.Maui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CalorieTracker.Maui.ViewModels;

/// <summary>Една AI оценка, която потребителят може да коригира преди да я добави.</summary>
public partial class AiItem(AiFoodEstimate estimate) : ObservableObject
{
    public AiFoodEstimate Estimate { get; } = estimate;

    public string Name => Estimate.Name;

    public string Per100g =>
        $"на 100 g: {Estimate.Calories:0} kcal · Б {Estimate.Protein:0.#} · В {Estimate.Carbs:0.#} · М {Estimate.Fat:0.#}";

    public string ConfidenceText => Estimate.Confidence switch
    {
        "high" => "Сигурност: висока",
        "medium" => "Сигурност: средна",
        _ => "Сигурност: ниска – проверете количеството"
    };

    public string? Note => Estimate.Note;
    public bool HasNote => !string.IsNullOrEmpty(Estimate.Note);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalText))]
    private string _gramsText = Labels.Number(estimate.Grams);

    public string TotalText =>
        Labels.TryParseNumber(GramsText, out var g) && g > 0
            ? $"{Estimate.ToFood().ForGrams(g).Calories:0} kcal"
            : "–";
}

public partial class AiFoodViewModel(CoreServices core, IFoodAiEstimator estimator) : ObservableObject, IQueryAttributable
{
    private DateOnly _date = DateOnly.FromDateTime(DateTime.Today);

    public IReadOnlyList<string> MealTypes => Labels.MealTypes;

    [ObservableProperty] private int _mealIndex;
    [ObservableProperty] private string _description = "";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _status = "Опишете какво сте яли (напр. „чиния мусака и шопска салата“) или снимайте храната.";
    [ObservableProperty] private ImageSource? _photo;

    public ObservableCollection<AiItem> Items { get; } = [];

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("date", out var date) && date is DateOnly d) _date = d;
        if (query.TryGetValue("meal", out var meal) && meal is int m) MealIndex = m;
        if (query.TryGetValue("text", out var text) && text is string t) Description = t;
        query.Clear();
    }

    [RelayCommand]
    private Task EstimateTextAsync()
    {
        if (string.IsNullOrWhiteSpace(Description))
            return Shell.Current.DisplayAlertAsync("AI", "Опишете храната.", "OK");

        Photo = null;
        return RunAsync(ct => estimator.EstimateFromTextAsync(Description, ct));
    }

    [RelayCommand]
    private Task TakePhotoAsync() => FromPhotoAsync(PhotoService.CaptureAsync);

    [RelayCommand]
    private Task PickPhotoAsync() => FromPhotoAsync(PhotoService.PickAsync);

    [RelayCommand]
    private async Task AddAsync(AiItem item)
    {
        if (await AddToDiaryAsync(item))
        {
            Items.Remove(item);
            if (Items.Count == 0) await Shell.Current.GoToAsync("../..");
        }
    }

    [RelayCommand]
    private async Task AddAllAsync()
    {
        foreach (var item in Items.ToList())
        {
            if (!await AddToDiaryAsync(item)) return;
            Items.Remove(item);
        }
        await Shell.Current.GoToAsync("../..");
    }

    private async Task FromPhotoAsync(Func<Task<PhotoData?>> source)
    {
        PhotoData? photo;
        try
        {
            photo = await source();
        }
        catch (Exception ex) when (ex is InvalidOperationException or FeatureNotSupportedException or PermissionException)
        {
            await Shell.Current.DisplayAlertAsync("Снимка", ex.Message, "OK");
            return;
        }
        if (photo is null) return;

        Photo = ImageSource.FromStream(() => new MemoryStream(photo.Bytes));
        await RunAsync(ct => estimator.EstimateFromPhotoAsync(photo.Bytes, photo.MediaType, Description, ct));
    }

    private async Task RunAsync(Func<CancellationToken, Task<IReadOnlyList<AiFoodEstimate>>> call)
    {
        if (!await estimator.IsConfiguredAsync())
        {
            var go = await Shell.Current.DisplayAlertAsync("Нужен е API ключ",
                "За AI оценката въведете Anthropic API ключ в раздел „Цели“ → „AI“.", "Към настройките", "Отказ");
            if (go) await Shell.Current.GoToAsync("//goals");
            return;
        }

        IsBusy = true;
        Status = "AI анализира храната…";
        Items.Clear();
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var estimates = await call(cts.Token);
            foreach (var e in estimates) Items.Add(new AiItem(e));
            Status = estimates.Count switch
            {
                0 => "AI не откри храна. Опитайте с по-подробно описание или друга снимка.",
                1 => "Проверете количеството и натиснете „Добави“.",
                _ => $"Открити са {estimates.Count} храни. Проверете количествата и ги добавете."
            };
        }
        catch (Exception ex) when (ex is AiEstimateException or AiNotConfiguredException or OperationCanceledException)
        {
            Status = ex is OperationCanceledException ? "AI не отговори навреме. Опитайте отново." : ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<bool> AddToDiaryAsync(AiItem item)
    {
        if (!Labels.TryParseNumber(item.GramsText, out var grams) || grams <= 0)
        {
            await Shell.Current.DisplayAlertAsync("Грешка", $"Въведете количество за „{item.Name}“.", "OK");
            return false;
        }

        var food = await core.RunAsync<FoodService, Food>(s => s.SaveExternalAsync(item.Estimate.ToFood()));
        await core.RunAsync<DiaryService, MealEntry>(s => s.AddEntryAsync(_date, (MealType)MealIndex, food.Id, grams));
        return true;
    }
}
