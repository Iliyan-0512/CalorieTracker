using CalorieTracker.Maui.Views;

namespace CalorieTracker.Maui;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute(nameof(AddFoodPage), typeof(AddFoodPage));
        Routing.RegisterRoute(nameof(CustomFoodPage), typeof(CustomFoodPage));
    }
}
