using CalorieTracker.Maui.ViewModels;

namespace CalorieTracker.Maui.Views;

public partial class AiFoodPage : ContentPage
{
    public AiFoodPage(AiFoodViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
