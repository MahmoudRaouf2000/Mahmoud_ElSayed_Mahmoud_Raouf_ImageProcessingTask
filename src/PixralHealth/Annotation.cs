using System.Drawing.Drawing2D;

namespace PixralHealth;

/// <summary>Endpoints and stroke width are measured in original image pixels.</summary>
public sealed record Annotation(PointF Start, PointF End, Color Color, float Width)
{
    public void Draw(Graphics graphics)
    {
        using var pen = new Pen(Color, Width)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        graphics.DrawLine(pen, Start, End);
    }
}
