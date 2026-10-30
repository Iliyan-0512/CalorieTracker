using CalorieTracker.Maui.ViewModels;

namespace CalorieTracker.Maui.Views;

public partial class CustomFoodPage : ContentPage
{
    public CustomFoodPage(CustomFoodViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
