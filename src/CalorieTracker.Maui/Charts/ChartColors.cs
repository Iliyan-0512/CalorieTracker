namespace CalorieTracker.Maui.Charts;

internal static class ChartColors
{
    private static bool IsDark => Application.Current?.RequestedTheme == AppTheme.Dark;

    public static Color Accent => IsDark ? Color.FromArgb("#7BD89F") : Color.FromArgb("#2E9E5B");
    public static Color Over => IsDark ? Color.FromArgb("#FF8A80") : Color.FromArgb("#D84343");
    public static Color Target => IsDark ? Color.FromArgb("#C8C8C8") : Color.FromArgb("#6E6E6E");
    public static Color Text => IsDark ? Color.FromArgb("#C8C8C8") : Color.FromArgb("#404040");
    public static Color Grid => IsDark ? Color.FromArgb("#404040") : Color.FromArgb("#E1E1E1");
}
