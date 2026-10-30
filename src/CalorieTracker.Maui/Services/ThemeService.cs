namespace CalorieTracker.Maui.Services;

public static class ThemeService
{
    private const string Key = "app_theme";

    /// <summary>0 = системна, 1 = светла, 2 = тъмна.</summary>
    public static int Current
    {
        get => Preferences.Default.Get(Key, 0);
        set
        {
            Preferences.Default.Set(Key, value);
            Apply();
        }
    }

    public static void Apply()
    {
        if (Application.Current is null) return;
        Application.Current.UserAppTheme = Current switch
        {
            1 => AppTheme.Light,
            2 => AppTheme.Dark,
            _ => AppTheme.Unspecified
        };
    }
}
