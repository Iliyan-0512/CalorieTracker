using CalorieTracker.Core.Services;

namespace CalorieTracker.Maui.Charts;

/// <summary>Стълбове с калориите по дни и пунктирана линия за целта.</summary>
public class CaloriesChartDrawable : IDrawable
{
    public IReadOnlyList<DayStat> Days { get; set; } = [];

    public void Draw(ICanvas canvas, RectF rect)
    {
        const float left = 8, bottom = 20, top = 16;
        var plot = new RectF(rect.X + left, rect.Y + top, rect.Width - left * 2, rect.Height - top - bottom);

        canvas.FontColor = ChartColors.Text;
        canvas.FontSize = 11;

        if (Days.Count == 0 || Days.All(d => d.Consumed.Calories == 0))
        {
            canvas.DrawString("Още няма записани хранения", rect, HorizontalAlignment.Center, VerticalAlignment.Center);
            return;
        }

        var target = Days[0].TargetCalories;
        var max = Math.Max(target * 1.2, Days.Max(d => d.Consumed.Calories) * 1.05);

        float Y(double value) => plot.Bottom - (float)(value / max) * plot.Height;

        var slot = plot.Width / Days.Count;
        var barWidth = Math.Max(2, slot * 0.65f);

        for (var i = 0; i < Days.Count; i++)
        {
            var day = Days[i];
            var x = plot.X + i * slot + (slot - barWidth) / 2;

            if (day.Consumed.Calories > 0)
            {
                canvas.FillColor = day.Consumed.Calories > target * 1.1 ? ChartColors.Over : ChartColors.Accent;
                var y = Y(day.Consumed.Calories);
                canvas.FillRoundedRectangle(x, y, barWidth, plot.Bottom - y, Math.Min(4, barWidth / 2));
            }

            // При много дни показваме само част от етикетите
            var labelEvery = Days.Count <= 7 ? 1 : Days.Count <= 31 ? 5 : 15;
            if (i % labelEvery == 0 || i == Days.Count - 1)
            {
                var label = Days.Count <= 7 ? day.Date.ToString("ddd", Services.Labels.Bg) : day.Date.ToString("d.M");
                canvas.DrawString(label, plot.X + i * slot - 10, plot.Bottom + 4, slot + 20, 14,
                    HorizontalAlignment.Center, VerticalAlignment.Top);
            }
        }

        // Линия на целта
        var targetY = Y(target);
        canvas.StrokeColor = ChartColors.Target;
        canvas.StrokeSize = 1.5f;
        canvas.StrokeDashPattern = [4, 4];
        canvas.DrawLine(plot.X, targetY, plot.Right, targetY);
        canvas.StrokeDashPattern = null;
        canvas.DrawString($"цел {target:0}", plot.Right - 80, targetY - 15, 80, 14, HorizontalAlignment.Right, VerticalAlignment.Top);
    }
}
