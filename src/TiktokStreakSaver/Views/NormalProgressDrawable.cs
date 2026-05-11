using Microsoft.Maui.Graphics;

namespace TiktokStreakSaver.Views;

/// <summary>
/// Custom drawable for the Normal Mode progress bar.
/// Renders a linear progress bar showing sent messages vs daily target,
/// using the Primary pink color.
/// </summary>
[Microsoft.Maui.Controls.Internals.Preserve(AllMembers = true)]
public class NormalProgressDrawable : IDrawable
{
    public float Progress { get; set; }

    public bool IsDarkTheme { get; set; }

    private static readonly Color TrackLight = Color.FromArgb("#E5E7EB");
    private static readonly Color TrackDark = Color.FromArgb("#2E3036");
    private static readonly Color FillColor = Color.FromArgb("#FE2C55"); // Primary pink

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var cornerRadius = dirtyRect.Height / 2f;

        canvas.FillColor = IsDarkTheme ? TrackDark : TrackLight;
        canvas.FillRoundedRectangle(dirtyRect, cornerRadius);

        if (Progress > 0)
        {
            var fillWidth = Math.Max(cornerRadius * 2, dirtyRect.Width * Progress);
            var fillRect = new RectF(dirtyRect.X, dirtyRect.Y, fillWidth, dirtyRect.Height);

            canvas.SetShadow(new SizeF(0, 2), 6, FillColor.WithAlpha(0.5f));

            var gradientPaint = new LinearGradientPaint
            {
                StartColor = Color.FromArgb("#FF5C7E"),
                EndColor = FillColor,
                StartPoint = new Point(0, 0.5),
                EndPoint = new Point(1, 0.5)
            };
            canvas.SetFillPaint(gradientPaint, fillRect);

            canvas.FillRoundedRectangle(fillRect, cornerRadius);

            canvas.SetShadow(SizeF.Zero, 0, Colors.Transparent);
        }
    }
}
