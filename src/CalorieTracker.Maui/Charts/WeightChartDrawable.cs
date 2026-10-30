using CalorieTracker.Core.Models;

namespace CalorieTracker.Maui.Charts;

/// <summary>Линейна графика на теглото във времето.</summary>
public class WeightChartDrawable : IDrawable
{
    public IReadOnlyList<WeightEntry> Weights { get; set; } = [];

    public void Draw(ICanvas canvas, RectF rect)
    {
        const float left = 40, right = 12, bottom = 20, top = 12;
        var plot = new RectF(rect.X + left, rect.Y + top, rect.Width - left - right, rect.Height - top - bottom);

        canvas.FontColor = ChartColors.Text;
        canvas.FontSize = 11;

        if (Weights.Count == 0)
        {
            canvas.DrawString("Въведете тегло, за да видите графиката", rect, HorizontalAlignment.Center, VerticalAlignment.Center);
            return;
        }

        var min = Weights.Min(w => w.WeightKg) - 1;
        var max = Weights.Max(w => w.WeightKg) + 1;
        var first = Weights[0].Date.DayNumber;
        var span = Math.Max(1, Weights[^1].Date.DayNumber - first);

        float X(WeightEntry w) => Weights.Count == 1 ? plot.Center.X : plot.X + (w.Date.DayNumber - first) / (float)span * plot.Width;
        float Y(double kg) => plot.Bottom - (float)((kg - min) / (max - min)) * plot.Height;

        // Хоризонтални линии и стойности по оста Y
        canvas.StrokeColor = ChartColors.Grid;
        canvas.StrokeSize = 1;
        for (var i = 0; i <= 3; i++)
        {
            var kg = min + (max - min) * i / 3;
            var y = Y(kg);
            canvas.DrawLine(plot.X, y, plot.Right, y);
            canvas.DrawString($"{kg:0.#}", rect.X, y - 7, left - 6, 14, HorizontalAlignment.Right, VerticalAlignment.Center);
        }

        canvas.StrokeColor = ChartColors.Accent;
        canvas.StrokeSize = 2.5f;
        var path = new PathF();
        for (var i = 0; i < Weights.Count; i++)
        {
            var point = new PointF(X(Weights[i]), Y(Weights[i].WeightKg));
            if (i == 0) path.MoveTo(point);
            else path.LineTo(point);
        }
        canvas.DrawPath(path);

        canvas.FillColor = ChartColors.Accent;
        foreach (var w in Weights)
            canvas.FillCircle(X(w), Y(w.WeightKg), 3.5f);

        canvas.DrawString(Weights[0].Date.ToString("d.M"), plot.X - 20, plot.Bottom + 4, 40, 14, HorizontalAlignment.Center, VerticalAlignment.Top);
        if (Weights.Count > 1)
            canvas.DrawString(Weights[^1].Date.ToString("d.M"), plot.Right - 20, plot.Bottom + 4, 40, 14, HorizontalAlignment.Center, VerticalAlignment.Top);
    }
}
