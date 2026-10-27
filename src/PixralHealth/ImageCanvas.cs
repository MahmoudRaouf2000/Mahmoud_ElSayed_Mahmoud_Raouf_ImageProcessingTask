using System.Drawing.Drawing2D;

namespace PixralHealth;

public class ImageCanvas : Control
{
    private readonly ViewportTransform viewport = new();
    private readonly TextureBrush checkerBrush;
    private ImageDocument? document;
    private MouseButtons activeButton;
    private Point lastMouse;
    private Point zoomOrigin;
    private float zoomStartScale;
    private Annotation? preview;
    private Size previousSize;
    private bool fitOnResize = true;

    public ImageCanvas()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        BackColor = Color.FromArgb(36, 41, 49);
        TabStop = true;
        using var tile = new Bitmap(24, 24);
        using (var graphics = Graphics.FromImage(tile))
        {
            graphics.Clear(Color.FromArgb(235, 235, 235));
            using var dark = new SolidBrush(Color.FromArgb(205, 205, 205));
            graphics.FillRectangle(dark, 0, 0, 12, 12);
            graphics.FillRectangle(dark, 12, 12, 12, 12);
        }
        checkerBrush = new TextureBrush(tile, WrapMode.Tile);
        Paint += RenderCanvas;
    }

    public event EventHandler? ViewChanged;
    public event EventHandler? AnnotationsChanged;
    public event Action<PointF?>? ImagePointerMoved;

    public float Zoom => viewport.Scale;
    public ImageDocument? Document
    {
        get => document;
        set
        {
            CancelInteraction();
            document = value;
            FitImage();
        }
    }

    public PointF ImageToScreen(PointF point) => viewport.ImageToScreen(point);
    public PointF ScreenToImage(PointF point) => viewport.ScreenToImage(point);

    public void FitImage()
    {
        CancelInteraction();
        fitOnResize = true;
        if (document is not null) viewport.Fit(document.CurrentImage.Size, ClientSize);
        Invalidate();
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RenderCanvas(object? sender, PaintEventArgs e)
    {
        var graphics = e.Graphics;
        if (document is null)
        {
            TextRenderer.DrawText(graphics, "Open an image to begin", Font, ClientRectangle,
                Color.FromArgb(195, 203, 214), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        var topLeft = viewport.ImageToScreen(PointF.Empty);
        var image = document.CurrentImage;
        var screenBounds = new RectangleF(topLeft.X, topLeft.Y, image.Width * Zoom, image.Height * Zoom);
        graphics.FillRectangle(checkerBrush, screenBounds);

        var state = graphics.Save();
        try
        {
            // The same matrix renders pixels and annotations; inverse conversion is used for mouse input.
            using var transform = new Matrix(Zoom, 0, 0, Zoom, topLeft.X, topLeft.Y);
            graphics.Transform = transform;
            graphics.SetClip(new Rectangle(0, 0, image.Width, image.Height));
            graphics.InterpolationMode = Zoom >= 4 ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            graphics.DrawImage(image, new Rectangle(0, 0, image.Width, image.Height),
                0, 0, image.Width, image.Height, GraphicsUnit.Pixel);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            foreach (var annotation in document.Annotations) annotation.Draw(graphics);
            preview?.Draw(graphics);
        }
        finally { graphics.Restore(state); }

        using var border = new Pen(Color.FromArgb(145, 156, 172));
        graphics.DrawRectangle(border, screenBounds.X, screenBounds.Y, screenBounds.Width, screenBounds.Height);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (document is null || activeButton != MouseButtons.None ||
            e.Button is not (MouseButtons.Left or MouseButtons.Middle or MouseButtons.Right)) return;
        var imagePoint = ScreenToImage(e.Location);
        if (e.Button == MouseButtons.Right && !ContainsImagePoint(imagePoint)) return;
        Focus();
        activeButton = e.Button;
        lastMouse = zoomOrigin = e.Location;
        zoomStartScale = Zoom;
        if (e.Button == MouseButtons.Right)
        {
            var start = ClampToImage(imagePoint);
            preview = new Annotation(start, start, Color.FromArgb(255, 220, 50, 50), 3f);
        }
        else fitOnResize = false;
        Capture = true;
        Cursor = e.Button switch
        {
            MouseButtons.Left => Cursors.SizeAll,
            MouseButtons.Middle => Cursors.SizeNS,
            _ => Cursors.Cross
        };
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (document is null) return;
        switch (activeButton)
        {
            case MouseButtons.Left:
                viewport.Pan(e.X - lastMouse.X, e.Y - lastMouse.Y);
                break;
            case MouseButtons.Middle:
                // Absolute drag distance avoids accumulated rounding and fixes the image's screen center.
                viewport.SetScale((float)(zoomStartScale * Math.Exp(((double)zoomOrigin.Y - e.Y) * 0.01)));
                break;
            case MouseButtons.Right when preview is not null:
                preview = preview with { End = ClampToImage(ScreenToImage(e.Location)) };
                break;
        }
        lastMouse = e.Location;
        var point = ScreenToImage(e.Location);
        ImagePointerMoved?.Invoke(ContainsImagePoint(point) ? point : null);
        if (activeButton == MouseButtons.None) return;
        Invalidate();
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (activeButton == MouseButtons.None || e.Button != activeButton) return;
        if (activeButton == MouseButtons.Right && preview is not null && document is not null)
        {
            preview = preview with { End = ClampToImage(ScreenToImage(e.Location)) };
            if (preview.Start != preview.End)
            {
                document.AddAnnotation(preview);
                AnnotationsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        CancelInteraction();
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (!Capture) CancelInteraction();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        ImagePointerMoved?.Invoke(null);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            CancelInteraction();
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (document is not null)
        {
            CancelInteraction();
            if (fitOnResize) viewport.Fit(document.CurrentImage.Size, ClientSize);
            else viewport.Pan((ClientSize.Width - previousSize.Width) / 2f,
                (ClientSize.Height - previousSize.Height) / 2f);
            ViewChanged?.Invoke(this, EventArgs.Empty);
        }
        previousSize = ClientSize;
    }

    private bool ContainsImagePoint(PointF point) => document is not null &&
        point.X >= 0 && point.Y >= 0 && point.X < document.CurrentImage.Width && point.Y < document.CurrentImage.Height;

    private PointF ClampToImage(PointF point) => new(
        Math.Clamp(point.X, 0, document!.CurrentImage.Width - 1),
        Math.Clamp(point.Y, 0, document.CurrentImage.Height - 1));

    private void CancelInteraction()
    {
        activeButton = MouseButtons.None;
        preview = null;
        if (Capture) Capture = false;
        Cursor = Cursors.Default;
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) checkerBrush.Dispose();
        base.Dispose(disposing);
    }
}
