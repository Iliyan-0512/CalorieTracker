using CalorieTracker.Maui.ViewModels;

namespace CalorieTracker.Maui.Views;

public partial class AddFoodPage : ContentPage
{
    private readonly AddFoodViewModel _viewModel;

    public AddFoodPage(AddFoodViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }
}
