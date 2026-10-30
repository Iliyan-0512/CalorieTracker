using CalorieTracker.Maui.Services;

namespace CalorieTracker.Maui;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        ThemeService.Apply();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell()) { Title = "CalorieTracker" };
    }
}
