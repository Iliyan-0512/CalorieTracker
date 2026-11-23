using System.Globalization;
using CalorieTracker.Core.Models;

namespace CalorieTracker.Maui.Services;

public class IsNotNullConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not null;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public class MoreThanOneConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is int n && n > 1;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Кратък етикет откъде е храната – показва се в списъка с резултати.</summary>
public class FoodSourceConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        FoodSource.Custom => "моя",
        FoodSource.OpenFoodFacts => "OpenFoodFacts",
        FoodSource.Usda => "USDA",
        FoodSource.AiEstimate => "AI",
        _ => "база"
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
