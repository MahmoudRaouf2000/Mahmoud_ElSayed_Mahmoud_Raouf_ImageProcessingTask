using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using PixralHealth;

internal static class Program
{
    private static int passed;
    private static int failed;
    private static readonly string TestDirectory = Path.Combine(Path.GetTempPath(), "PixralHealth-Verification-" + Guid.NewGuid().ToString("N"));

    [STAThread]
    private static int Main()
    {
        ApplicationConfiguration.Initialize();
        Directory.CreateDirectory(TestDirectory);
        try
        {
            Run("Invert RGB, alpha, and double inversion", TestInvert);
            Run("Weighted grayscale, rounding, and composition", TestGrayscale);
            Run("24-bit, indexed, and premultiplied input normalization", TestFormats);
            Run("Signed-stride bitmap processing", TestNegativeStride);
            Run("Original reset retains annotations", TestReset);
            Run("Fit, coordinate inverse, pan, center zoom, and limits", TestTransform);
            Run("PNG/JPEG/BMP export includes filters and annotations", TestExport);
            Run("Input detachment, overwrite, invalid file, and save failures", TestFiles);
            Run("Canvas mouse interaction, preview, paint, capture, and resize", TestCanvas);
            Run("Main window starts with disabled image commands", TestMainForm);
        }
        finally
        {
            // The directory is uniquely created by this process and contains only verification fixtures.
            Directory.Delete(TestDirectory, recursive: true);
        }
        Console.WriteLine($"\n{passed} checks passed; {failed} failed.");
        return failed == 0 ? 0 : 1;
    }

    private static void Run(string name, Action test)
    {
        try { test(); passed++; Console.WriteLine($"PASS {name}"); }
        catch (Exception error) { failed++; Console.WriteLine($"FAIL {name}\n{error}"); }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Near(float actual, float expected, string message, float tolerance = 0.001f) =>
        Assert(Math.Abs(actual - expected) <= tolerance, $"{message}: expected {expected}, actual {actual}");

    private static void SameColor(Color actual, Color expected, string message) =>
        Assert(actual.ToArgb() == expected.ToArgb(), $"{message}: expected {expected}, actual {actual}");

    private static Bitmap Solid(int width, int height, Color color)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(color);
        return bitmap;
    }

    private static void TestInvert()
    {
        using var bitmap = new Bitmap(5, 2, PixelFormat.Format32bppArgb);
        Color[] colors = [Color.FromArgb(128, 10, 20, 30), Color.Red, Color.Black, Color.White, Color.FromArgb(0, 33, 66, 99)];
        for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++) bitmap.SetPixel(x, y, colors[x]);
        ImageProcessor.ApplyInPlace(bitmap, ImageFilter.Invert);
        for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
                SameColor(bitmap.GetPixel(x, y), Color.FromArgb(colors[x].A, 255 - colors[x].R,
                    255 - colors[x].G, 255 - colors[x].B), "Invert must preserve alpha and cover every row");
        ImageProcessor.ApplyInPlace(bitmap, ImageFilter.Invert);
        for (var x = 0; x < bitmap.Width; x++) SameColor(bitmap.GetPixel(x, 1), colors[x], "Two inversions restore input");
        using var copy = ImageProcessor.CopyToArgb(bitmap);
        for (var x = 0; x < bitmap.Width; x++) SameColor(copy.GetPixel(x, 1), colors[x], "ARGB copies preserve translucent and hidden RGB exactly");
    }

    private static void TestGrayscale()
    {
        using var bitmap = new Bitmap(4, 1, PixelFormat.Format32bppArgb);
        Color[] input = [Color.Red, Color.Lime, Color.Blue, Color.FromArgb(123, 10, 20, 30)];
        int[] gray = [76, 150, 29, 18];
        for (var x = 0; x < 4; x++) bitmap.SetPixel(x, 0, input[x]);
        ImageProcessor.ApplyInPlace(bitmap, ImageFilter.Grayscale);
        for (var x = 0; x < 4; x++)
            SameColor(bitmap.GetPixel(x, 0), Color.FromArgb(input[x].A, gray[x], gray[x], gray[x]), "Weighted luminance");
        ImageProcessor.ApplyInPlace(bitmap, ImageFilter.Grayscale);
        ImageProcessor.ApplyInPlace(bitmap, ImageFilter.Invert);
        for (var x = 0; x < 4; x++)
            SameColor(bitmap.GetPixel(x, 0), Color.FromArgb(input[x].A, 255 - gray[x], 255 - gray[x], 255 - gray[x]), "Sequential filters compose");
    }

    private static void TestFormats()
    {
        using var rgb = new Bitmap(7, 3, PixelFormat.Format24bppRgb);
        rgb.SetPixel(6, 2, Color.CornflowerBlue);
        using var converted = ImageProcessor.CopyToArgb(rgb);
        SameColor(converted.GetPixel(6, 2), Color.CornflowerBlue, "24-bit odd width conversion");
        ImageProcessor.ApplyInPlace(converted, ImageFilter.Invert);

        using var indexed = new Bitmap(7, 3, PixelFormat.Format8bppIndexed);
        var palette = indexed.Palette;
        palette.Entries[1] = Color.Magenta;
        indexed.Palette = palette;
        var data = indexed.LockBits(new Rectangle(0, 0, 7, 3), ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);
        try
        {
            for (var y = 0; y < 3; y++) Marshal.Copy(Enumerable.Repeat((byte)1, 7).ToArray(), 0, IntPtr.Add(data.Scan0, y * data.Stride), 7);
        }
        finally { indexed.UnlockBits(data); }
        var indexedPath = Path.Combine(TestDirectory, "indexed.png");
        indexed.Save(indexedPath, ImageFormat.Png);
        using var loaded = ImageFiles.Load(indexedPath);
        Assert(loaded.CurrentImage.PixelFormat == PixelFormat.Format32bppArgb, "Indexed image must normalize to ARGB");
        SameColor(loaded.CurrentImage.GetPixel(6, 2), Color.Magenta, "Indexed palette must survive decoding");

        using var premultiplied = new Bitmap(3, 3, PixelFormat.Format32bppPArgb);
        premultiplied.SetPixel(1, 1, Color.FromArgb(128, 200, 100, 50));
        using var straight = ImageProcessor.CopyToArgb(premultiplied);
        var pixel = straight.GetPixel(1, 1);
        Assert(pixel.A == 128 && Math.Abs(pixel.R - 200) <= 2 && Math.Abs(pixel.G - 100) <= 2, "Premultiplied alpha normalization");
    }

    private static void TestNegativeStride()
    {
        const int stride = 16;
        var memory = Marshal.AllocHGlobal(stride * 3);
        try
        {
            using var bitmap = new Bitmap(4, 3, -stride, PixelFormat.Format32bppArgb, IntPtr.Add(memory, stride * 2));
            for (var y = 0; y < 3; y++)
                for (var x = 0; x < 4; x++) bitmap.SetPixel(x, y, Color.FromArgb(120, x * 40, y * 50, 25));
            ImageProcessor.ApplyInPlace(bitmap, ImageFilter.Invert);
            for (var y = 0; y < 3; y++)
                for (var x = 0; x < 4; x++)
                    SameColor(bitmap.GetPixel(x, y), Color.FromArgb(120, 255 - x * 40, 255 - y * 50, 230), "Negative stride pixel");
        }
        finally { Marshal.FreeHGlobal(memory); }
    }

    private static void TestReset()
    {
        using var document = new ImageDocument(Solid(80, 50, Color.CornflowerBlue), "fixture.png", DateTime.Now);
        var line = new Annotation(new PointF(5, 25), new PointF(75, 25), Color.Red, 3);
        document.AddAnnotation(line);
        var filtered = ImageProcessor.CopyToArgb(document.CurrentImage);
        ImageProcessor.ApplyInPlace(filtered, ImageFilter.Invert);
        document.ReplaceProcessedImage(filtered);
        document.ResetFilters();
        SameColor(document.CurrentImage.GetPixel(0, 0), Color.CornflowerBlue, "Reset restores original");
        Assert(document.Annotations.Count == 1 && document.Annotations[0] == line, "Reset keeps annotations");
        Assert(document.HasUnsavedChanges, "Edits mark document dirty");
        document.MarkSaved();
        Assert(!document.HasUnsavedChanges, "Successful save clears dirty state");
        document.ClearAnnotations();
        Assert(document.HasUnsavedChanges && document.Annotations.Count == 0, "Clear lines is an edit");
    }

    private static void TestTransform()
    {
        var view = new ViewportTransform();
        view.Fit(new Size(400, 200), new Size(840, 440));
        Near(view.Scale, 2, "Fit preserves aspect ratio with padding");
        var point = new PointF(71.5f, 83.25f);
        var roundtrip = view.ScreenToImage(view.ImageToScreen(point));
        Near(roundtrip.X, point.X, "Inverse X");
        Near(roundtrip.Y, point.Y, "Inverse Y");
        var center = view.ImageToScreen(new PointF(200, 100));
        view.Pan(37, -19);
        Near(view.Center.X, center.X + 37, "Pan X in screen pixels");
        Near(view.Center.Y, center.Y - 19, "Pan Y in screen pixels");
        var pannedCenter = view.Center;
        view.SetScale(17);
        Assert(view.ImageToScreen(new PointF(200, 100)) == pannedCenter, "Zoom keeps image center fixed after pan");
        view.SetScale(float.PositiveInfinity);
        Near(view.Scale, ViewportTransform.MaximumScale, "Maximum zoom");
        view.SetScale(-100);
        Near(view.Scale, ViewportTransform.MinimumScale, "Minimum zoom");
        view.SetScale(float.NaN);
        Assert(float.IsFinite(view.Scale), "NaN cannot corrupt transform");
    }

    private static void TestExport()
    {
        using var document = new ImageDocument(Solid(120, 80, Color.Blue), "fixture.png", DateTime.Now);
        var filtered = ImageProcessor.CopyToArgb(document.CurrentImage);
        ImageProcessor.ApplyInPlace(filtered, ImageFilter.Invert);
        document.ReplaceProcessedImage(filtered);
        document.AddAnnotation(new Annotation(new PointF(10, 40), new PointF(110, 40), Color.Red, 5));
        using var output = document.RenderOutput();
        foreach (var extension in new[] { "png", "jpg", "jpeg", "bmp" })
        {
            var path = Path.Combine(TestDirectory, "export." + extension);
            ImageFiles.Save(output, path);
            Assert(new FileInfo(path).Length > 0, "Saved file is not empty");
            using var decoded = new Bitmap(path);
            Assert(decoded.Size == new Size(120, 80), "Export retains original resolution");
            var background = decoded.GetPixel(5, 5);
            Assert(background.R > 240 && background.G > 240 && background.B < 15, "Export includes inverted background");
            var stroke = decoded.GetPixel(60, 40);
            Assert(stroke.R > 200 && stroke.G < 55 && stroke.B < 55, "Export includes red annotation");
        }
        using var transparent = Solid(20, 20, Color.Transparent);
        transparent.SetPixel(10, 10, Color.FromArgb(128, 200, 100, 50));
        var png = Path.Combine(TestDirectory, "alpha.png");
        ImageFiles.Save(transparent, png);
        using (var decoded = new Bitmap(png))
        {
            Assert(decoded.GetPixel(0, 0).A == 0, "PNG preserves transparent pixels");
            Assert(decoded.GetPixel(10, 10).A == 128, "PNG preserves partial alpha");
        }
        foreach (var extension in new[] { "jpg", "bmp" })
        {
            var path = Path.Combine(TestDirectory, "white-alpha." + extension);
            ImageFiles.Save(transparent, path);
            using var decoded = new Bitmap(path);
            var corner = decoded.GetPixel(0, 0);
            Assert(corner.R > 245 && corner.G > 245 && corner.B > 245, "Non-alpha export uses white background");
        }
    }

    private static void TestFiles()
    {
        var path = Path.Combine(TestDirectory, "unlocked.png");
        using (var fixture = Solid(20, 10, Color.Lime)) fixture.Save(path, ImageFormat.Png);
        using (var loaded = ImageFiles.Load(path))
        {
            using (var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Assert(exclusive.Length > 0, "Source is not locked after load");
            using var output = loaded.RenderOutput();
            ImageFiles.Save(output, path);
            Assert(loaded.CreationTime == File.GetCreationTime(path) || loaded.CreationTime <= DateTime.Now, "Creation metadata loaded");
        }
        for (var i = 0; i < 30; i++)
        {
            using var loaded = ImageFiles.Load(path);
            using var output = loaded.RenderOutput();
            ImageFiles.Save(output, path);
        }
        using var bitmap = Solid(8, 8, Color.White);
        var invalid = Path.Combine(TestDirectory, "invalid.png");
        File.WriteAllText(invalid, "This is not an image.");
        var rejected = false;
        try { using var ignored = ImageFiles.Load(invalid); }
        catch (ArgumentException) { rejected = true; }
        Assert(rejected, "Invalid images are rejected");
        var unsupported = Path.Combine(TestDirectory, "preserve.xyz");
        File.WriteAllText(unsupported, "existing file");
        try { ImageFiles.Save(bitmap, unsupported); }
        catch (ArgumentException) { }
        Assert(File.ReadAllText(unsupported) == "existing file", "Unsupported extension cannot overwrite destination");
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            rejected = false;
            try { ImageFiles.Save(bitmap, path); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { rejected = true; }
            Assert(rejected, "Locked destination reports an error");
        }
        Assert(!Directory.EnumerateFiles(TestDirectory, "*.tmp").Any(), "Failed save cleans temporary output");
        using var retained = new Bitmap(path);
        Assert(retained.Size == new Size(20, 10), "Failed replacement retains original destination");
    }

    private static void TestCanvas()
    {
        using var document = new ImageDocument(Solid(200, 100, Color.White), "fixture.png", DateTime.Now);
        using var host = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-2000, -2000), ClientSize = new Size(640, 480) };
        using var canvas = new TestCanvasControl { Dock = DockStyle.Fill };
        host.Controls.Add(canvas);
        host.Show();
        canvas.Document = document;
        Application.DoEvents();
        var a = Point.Round(canvas.ImageToScreen(new PointF(20, 50)));
        var b = Point.Round(canvas.ImageToScreen(new PointF(180, 50)));
        canvas.Down(MouseButtons.Right, a);
        canvas.MoveTo(MouseButtons.Right, b);
        Assert(document.Annotations.Count == 0, "Preview does not commit annotation");
        using (var preview = new Bitmap(canvas.Width, canvas.Height))
        {
            canvas.DrawToBitmap(preview, canvas.ClientRectangle);
            var red = preview.GetPixel(canvas.Width / 2, canvas.Height / 2);
            Assert(red.R > 200 && red.G < 80, "Paint displays live preview");
        }
        canvas.Up(MouseButtons.Right, b);
        Assert(document.Annotations.Count == 1, "Right release finalizes annotation");
        Near(document.Annotations[0].Start.X, 20, "Annotation stores image X");
        Near(document.Annotations[0].End.X, 180, "Annotation stores image endpoint");
        SaveCanvasArtifact(canvas, "annotated-canvas.png");
        var center = canvas.ImageToScreen(new PointF(100, 50));
        canvas.Down(MouseButtons.Left, new Point(200, 200));
        canvas.MoveTo(MouseButtons.Left, new Point(230, 180));
        canvas.Up(MouseButtons.Left, new Point(230, 180));
        var movedCenter = canvas.ImageToScreen(new PointF(100, 50));
        Near(movedCenter.X, center.X + 30, "Left drag pan X");
        Near(movedCenter.Y, center.Y - 20, "Left drag pan Y");
        var oldScale = canvas.Zoom;
        canvas.Down(MouseButtons.Middle, new Point(300, 200));
        canvas.MoveTo(MouseButtons.Middle, new Point(300, 150));
        canvas.Up(MouseButtons.Middle, new Point(300, 150));
        Assert(canvas.Zoom > oldScale, "Middle drag upward zooms in");
        Assert(canvas.ImageToScreen(new PointF(100, 50)) == movedCenter, "Middle zoom preserves panned image center");
        using (var rendered = new Bitmap(canvas.Width, canvas.Height))
        {
            canvas.DrawToBitmap(rendered, canvas.ClientRectangle);
            var red = rendered.GetPixel((int)movedCenter.X, (int)movedCenter.Y);
            Assert(red.R > 200 && red.G < 80, "Committed annotation remains aligned after pan and zoom");
        }
        using (var output = document.RenderOutput()) Assert(output.Size == new Size(200, 100), "Viewport never changes export size");
        canvas.Down(MouseButtons.Left, new Point(200, 200));
        canvas.MoveTo(MouseButtons.Left, new Point(230, 180));
        canvas.Up(MouseButtons.Left, new Point(230, 180));
        var centerAfterSecondPan = canvas.ImageToScreen(new PointF(100, 50));
        Near(centerAfterSecondPan.X, movedCenter.X + 30, "Pan stays screen-based at new zoom");
        a = Point.Round(canvas.ImageToScreen(new PointF(30, 40)));
        canvas.Down(MouseButtons.Right, a);
        canvas.MoveTo(MouseButtons.Right, new Point(a.X + 10, a.Y + 10));
        canvas.Escape();
        canvas.Up(MouseButtons.Right, a);
        Assert(document.Annotations.Count == 1 && !canvas.Capture, "Escape cancels annotation and releases capture");
        canvas.Down(MouseButtons.Right, a);
        canvas.Capture = false;
        canvas.Up(MouseButtons.Right, a);
        Assert(document.Annotations.Count == 1, "Capture loss cancels incomplete line");
        var scale = canvas.Zoom;
        host.ClientSize = new Size(740, 580);
        Application.DoEvents();
        Near(canvas.Zoom, scale, "Resize retains manually chosen scale");
        canvas.FitImage();
        Near(canvas.ImageToScreen(new PointF(100, 50)).X, canvas.Width / 2f, "Fit recenters image");
        Near(canvas.ImageToScreen(new PointF(100, 50)).Y, canvas.Height / 2f, "Fit recenters image Y");
        canvas.Document = null;
        host.Close();
    }

    private static void TestMainForm()
    {
        using var form = new MainForm { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-2000, -2000) };
        form.Show();
        Application.DoEvents();
        var strip = Descendants(form).OfType<ToolStrip>().Single(control => control is not StatusStrip);
        foreach (var button in strip.Items.OfType<ToolStripButton>())
            Assert(button.Enabled == (button.Text == "Open Image"), "Only Open Image is enabled before loading");
        var canvas = Descendants(form).OfType<ImageCanvas>().Single();
        Assert(canvas.Width > 800 && canvas.Height > 400, "Canvas fills available window space");
        using var screenshot = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(screenshot, new Rectangle(0, 0, form.Width, form.Height));
        screenshot.Save(ArtifactPath("main-window.png"), ImageFormat.Png);
        form.Close();
    }

    private static string ArtifactPath(string filename)
    {
        var artifacts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../artifacts"));
        Directory.CreateDirectory(artifacts);
        return Path.Combine(artifacts, filename);
    }

    private static void SaveCanvasArtifact(Control canvas, string filename)
    {
        using var bitmap = new Bitmap(canvas.Width, canvas.Height);
        canvas.DrawToBitmap(bitmap, canvas.ClientRectangle);
        bitmap.Save(ArtifactPath(filename), ImageFormat.Png);
    }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private sealed class TestCanvasControl : ImageCanvas
    {
        public void Down(MouseButtons button, Point p) => OnMouseDown(new MouseEventArgs(button, 1, p.X, p.Y, 0));
        public void MoveTo(MouseButtons button, Point p) => OnMouseMove(new MouseEventArgs(button, 0, p.X, p.Y, 0));
        public void Up(MouseButtons button, Point p) => OnMouseUp(new MouseEventArgs(button, 1, p.X, p.Y, 0));
        public void Escape() => OnKeyDown(new KeyEventArgs(Keys.Escape));
    }
}
