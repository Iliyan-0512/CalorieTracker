using CalorieTracker.Maui.ViewModels;

namespace CalorieTracker.Maui.Views;

public partial class ProgressPage : ContentPage
{
    private readonly ProgressViewModel _viewModel;

    public ProgressPage(ProgressViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _viewModel.ChartsChanged += (_, _) =>
        {
            CaloriesView.Invalidate();
            WeightView.Invalidate();
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }
}
