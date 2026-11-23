using CalorieTracker.Maui.Views;

namespace CalorieTracker.Maui;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute(nameof(AddFoodPage), typeof(AddFoodPage));
        Routing.RegisterRoute(nameof(CustomFoodPage), typeof(CustomFoodPage));
        Routing.RegisterRoute(nameof(ScanBarcodePage), typeof(ScanBarcodePage));
        Routing.RegisterRoute(nameof(AiFoodPage), typeof(AiFoodPage));
    }
}
