using Microsoft.Maui.Graphics;

namespace TiktokStreakSaver.Views;

/// <summary>
/// Custom drawable for the circular success rate chart on History.
/// Renders a donut-style arc showing the % of successful runs.
/// </summary>
[Microsoft.Maui.Controls.Internals.Preserve(AllMembers = true)]
public class SuccessRateDrawable : IDrawable
{
    public float SuccessRate { get; set; }
    public string RateText { get; set; } = "—";
    public string SubText { get; set; } = "";

    private static readonly Color TrackLight = Color.FromArgb("#E5E7EB");
    private static readonly Color TrackDark = Color.FromArgb("#2E3036");
    private static readonly Color ArcColor = Color.FromArgb("#22C55E");
    private static readonly Color TextLight = Color.FromArgb("#141517");
    private static readonly Color TextDark = Color.FromArgb("#FFFFFF");

    public bool IsDarkTheme { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var cx = dirtyRect.Width / 2f;
        var cy = dirtyRect.Height / 2f;
        var radius = Math.Min(cx, cy) - 10f;
        var stroke = 7f;

        canvas.StrokeColor = IsDarkTheme ? TrackDark : TrackLight;
        canvas.StrokeSize = stroke;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawCircle(cx, cy, radius);

        if (SuccessRate > 0.005f)
        {
            canvas.StrokeColor = ArcColor;
            canvas.StrokeSize = stroke;
            canvas.StrokeLineCap = LineCap.Round;

            if (SuccessRate >= 0.999f)
            {
                canvas.DrawCircle(cx, cy, radius);
            }
            else
            {
                var sweepAngle = SuccessRate * 360f;
                canvas.DrawArc(
                    cx - radius, cy - radius,
                    radius * 2, radius * 2,
                    90f,
                    90f - sweepAngle,
                    true, false);
            }
        }

        var textColor = IsDarkTheme ? TextDark : TextLight;
        canvas.FontColor = textColor;
        canvas.FontSize = 18;
        canvas.Font = Microsoft.Maui.Graphics.Font.Default;

        var textY = string.IsNullOrEmpty(SubText) ? cy : cy - 6;
        canvas.DrawString(RateText, 0, textY - 10, dirtyRect.Width, 24,
            HorizontalAlignment.Center, VerticalAlignment.Center);

        if (!string.IsNullOrEmpty(SubText))
        {
            canvas.FontSize = 10;
            canvas.FontColor = Color.FromArgb("#8B8F96");
            canvas.DrawString(SubText, 0, textY + 10, dirtyRect.Width, 20,
                HorizontalAlignment.Center, VerticalAlignment.Center);
        }
    }
}
