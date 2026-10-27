namespace PixralHealth;

/// <summary>screen = imageCenterOnScreen + (imagePoint - imageSize / 2) * scale.</summary>
public sealed class ViewportTransform
{
    public const float MinimumScale = 0.0001f;
    public const float MaximumScale = 64f;
    public float Scale { get; private set; } = 1;
    public PointF Center { get; private set; }
    public Size ImageSize { get; private set; }

    public void Fit(Size imageSize, Size viewportSize)
    {
        ImageSize = imageSize;
        Center = new PointF(viewportSize.Width / 2f, viewportSize.Height / 2f);
        if (imageSize.Width <= 0 || imageSize.Height <= 0) return;
        var availableWidth = Math.Max(1, viewportSize.Width - 40);
        var availableHeight = Math.Max(1, viewportSize.Height - 40);
        SetScale(Math.Min((float)availableWidth / imageSize.Width, (float)availableHeight / imageSize.Height));
    }

    public PointF ImageToScreen(PointF point) => new(
        Center.X + (point.X - ImageSize.Width / 2f) * Scale,
        Center.Y + (point.Y - ImageSize.Height / 2f) * Scale);

    public PointF ScreenToImage(PointF point) => new(
        (point.X - Center.X) / Scale + ImageSize.Width / 2f,
        (point.Y - Center.Y) / Scale + ImageSize.Height / 2f);

    public void Pan(float dx, float dy) => Center = new PointF(Center.X + dx, Center.Y + dy);

    public void SetScale(float scale)
    {
        if (float.IsNaN(scale)) return;
        Scale = Math.Clamp(scale, MinimumScale, MaximumScale);
    }
}
