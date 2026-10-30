using System.Globalization;
using CalorieTracker.Core.Models;

namespace CalorieTracker.Maui.Services;

public static class Labels
{
    public static readonly CultureInfo Bg = CultureInfo.GetCultureInfo("bg-BG");

    public static IReadOnlyList<string> MealTypes { get; } = ["Закуска", "Обяд", "Вечеря", "Междинно"];
    public static IReadOnlyList<string> Sexes { get; } = ["Мъж", "Жена"];
    public static IReadOnlyList<string> ActivityLevels { get; } =
        ["Заседнал (без спорт)", "Лека активност (1–3 пъти/седм.)", "Умерена (3–5 пъти/седм.)", "Висока (6–7 пъти/седм.)", "Много висока (физ. труд)"];
    public static IReadOnlyList<string> Goals { get; } = ["Отслабване", "Поддържане", "Качване"];
    public static IReadOnlyList<string> Themes { get; } = ["Системна", "Светла", "Тъмна"];
    public static IReadOnlyList<string> Periods { get; } = ["7 дни", "30 дни", "90 дни"];

    public static string MealName(MealType type) => MealTypes[(int)type];

    public static string DayTitle(DateOnly date)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (date == today) return "Днес";
        if (date == today.AddDays(-1)) return "Вчера";
        if (date == today.AddDays(1)) return "Утре";
        return date.ToString("dddd, d MMMM", Bg);
    }

    public static string Kcal(double value) => $"{Math.Round(value):0} kcal";

    public static string Grams(double value) => $"{Math.Round(value, 1):0.#} g";

    /// <summary>Приема както "1.5", така и "1,5" – на телефон с български език клавиатурата често дава запетая.</summary>
    public static bool TryParseNumber(string? text, out double value) =>
        double.TryParse(text?.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    public static string Number(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);
}
