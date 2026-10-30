using CalorieTracker.Maui.ViewModels;

namespace CalorieTracker.Maui.Views;

public partial class TodayPage : ContentPage
{
    private readonly DiaryViewModel _viewModel;

    public TodayPage(DiaryViewModel viewModel)
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
