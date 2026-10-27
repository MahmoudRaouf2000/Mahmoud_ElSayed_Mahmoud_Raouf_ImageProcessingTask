namespace PixralHealth;

public sealed class MainForm : Form
{
    private const string ApplicationTitle = "Image Processing Task for Applicants";
    private readonly Font interfaceFont = new("Segoe UI", 10);
    private readonly ImageCanvas canvas = new() { Dock = DockStyle.Fill };
    private readonly ToolStrip toolbar = new() { GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(8, 6, 8, 6) };
    private readonly ToolStripButton openButton = new("Open Image");
    private readonly ToolStripButton invertButton = new("Invert");
    private readonly ToolStripButton grayscaleButton = new("Grayscale");
    private readonly ToolStripButton resetButton = new("Reset Filters");
    private readonly ToolStripButton fitButton = new("Fit Image");
    private readonly ToolStripButton clearButton = new("Clear Lines");
    private readonly ToolStripButton saveButton = new("Save Image");
    private readonly ToolStripStatusLabel dimensionsLabel = new();
    private readonly ToolStripStatusLabel createdLabel = new();
    private readonly ToolStripStatusLabel zoomLabel = new();
    private readonly ToolStripStatusLabel pointerLabel = new();
    private readonly ToolStripStatusLabel messageLabel = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private ImageDocument? document;
    private bool busy;

    public MainForm()
    {
        Text = ApplicationTitle;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(940, 600);
        ClientSize = new Size(1180, 780);
        Font = interfaceFont;
        AutoScaleMode = AutoScaleMode.Dpi;
        KeyPreview = true;

        toolbar.Items.AddRange([openButton, new ToolStripSeparator(), invertButton, grayscaleButton,
            resetButton, new ToolStripSeparator(), fitButton, clearButton, new ToolStripSeparator(), saveButton]);
        foreach (ToolStripItem item in toolbar.Items)
        {
            if (item is ToolStripButton button)
            {
                button.DisplayStyle = ToolStripItemDisplayStyle.Text;
                button.Padding = new Padding(7, 4, 7, 4);
            }
        }
        openButton.ToolTipText = "Open an image (Ctrl+O)";
        invertButton.ToolTipText = "Invert the current RGB channels; preserves alpha";
        grayscaleButton.ToolTipText = "Apply weighted luminance to the current colors";
        resetButton.ToolTipText = "Restore original colors and keep all lines";
        fitButton.ToolTipText = "Fit and center the image (Ctrl+0)";
        clearButton.ToolTipText = "Remove all line annotations";
        saveButton.ToolTipText = "Save filters and lines at original resolution (Ctrl+S)";

        var instructions = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(12, 8, 12, 8),
            Text = "Left drag: pan   |   Middle drag up/down: zoom about image center   |   Right drag: draw red line   |   Esc: cancel drag",
            BackColor = Color.FromArgb(242, 245, 249)
        };
        var status = new StatusStrip { SizingGrip = true };
        status.Items.AddRange([messageLabel, dimensionsLabel, createdLabel, zoomLabel, pointerLabel]);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Margin = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        toolbar.Dock = DockStyle.Fill;
        toolbar.Margin = Padding.Empty;
        canvas.Margin = Padding.Empty;
        status.Dock = DockStyle.Fill;
        layout.Controls.Add(toolbar, 0, 0);
        layout.Controls.Add(instructions, 0, 1);
        layout.Controls.Add(canvas, 0, 2);
        layout.Controls.Add(status, 0, 3);
        Controls.Add(layout);

        openButton.Click += async (_, _) => await OpenImageAsync();
        saveButton.Click += async (_, _) => await SaveImageAsync();
        invertButton.Click += async (_, _) => await FilterAsync(ImageFilter.Invert);
        grayscaleButton.Click += async (_, _) => await FilterAsync(ImageFilter.Grayscale);
        resetButton.Click += async (_, _) => await RunBusyAsync("Restoring colors…", () =>
        {
            document!.ResetFilters();
            canvas.Invalidate();
            messageLabel.Text = "Original colors restored. Lines retained.";
            return Task.CompletedTask;
        });
        fitButton.Click += (_, _) => canvas.FitImage();
        clearButton.Click += (_, _) =>
        {
            document!.ClearAnnotations();
            canvas.Invalidate();
            UpdateControls();
        };
        canvas.ViewChanged += (_, _) => UpdateZoom();
        canvas.AnnotationsChanged += (_, _) => UpdateControls();
        canvas.ImagePointerMoved += point => pointerLabel.Text = point is { } p ? $"  X: {p.X:F0}  Y: {p.Y:F0}" : "";
        messageLabel.Text = "Open an image to begin.";
        UpdateControls();
    }

    private async Task OpenImageAsync()
    {
        if (busy || !ConfirmDiscard()) return;
        using var dialog = new OpenFileDialog
        {
            Title = "Open image",
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|All files|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        await RunBusyAsync("Opening image…", async () =>
        {
            var loaded = await Task.Run(() => ImageFiles.Load(dialog.FileName));
            var previous = document;
            document = loaded;
            canvas.Document = loaded;
            previous?.Dispose();
            dimensionsLabel.Text = $"  {loaded.CurrentImage.Width:N0} × {loaded.CurrentImage.Height:N0} px";
            createdLabel.Text = $"  Created: {loaded.CreationTime:yyyy-MM-dd HH:mm:ss}";
            messageLabel.Text = Path.GetFileName(loaded.FilePath);
            messageLabel.ToolTipText = loaded.FilePath;
        });
    }

    private async Task FilterAsync(ImageFilter filter)
    {
        if (busy || document is null) return;
        await RunBusyAsync($"Applying {filter.ToString().ToLowerInvariant()}…", async () =>
        {
            Bitmap? filtered = ImageProcessor.CopyToArgb(document.CurrentImage);
            try
            {
                await Task.Run(() => ImageProcessor.ApplyInPlace(filtered, filter));
                document.ReplaceProcessedImage(filtered);
                filtered = null; // Ownership now belongs to the document.
            }
            finally { filtered?.Dispose(); }
            canvas.Invalidate();
            messageLabel.Text = $"{filter} applied to current colors.";
        });
    }

    private async Task SaveImageAsync()
    {
        if (busy || document is null) return;
        using var dialog = new SaveFileDialog
        {
            Title = "Save processed image with annotations",
            Filter = "PNG image (*.png)|*.png|JPEG image (*.jpg;*.jpeg)|*.jpg;*.jpeg|Bitmap image (*.bmp)|*.bmp",
            DefaultExt = "png",
            AddExtension = true,
            OverwritePrompt = true,
            FileName = Path.GetFileNameWithoutExtension(document.FilePath) + "_processed"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        await RunBusyAsync("Saving image…", async () =>
        {
            using var output = document.RenderOutput();
            await Task.Run(() => ImageFiles.Save(output, dialog.FileName));
            document.MarkSaved();
            messageLabel.Text = $"Saved: {Path.GetFileName(dialog.FileName)}";
            MessageBox.Show(this, $"Image saved successfully.\n\n{dialog.FileName}\n\n{output.Width:N0} × {output.Height:N0} pixels",
                "Image saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
    }

    private async Task RunBusyAsync(string message, Func<Task> operation)
    {
        if (busy) return;
        busy = true;
        messageLabel.Text = message;
        UseWaitCursor = true;
        UpdateControls();
        try
        {
            await operation();
        }
        catch (Exception exception) when (exception is not (StackOverflowException or AccessViolationException))
        {
            messageLabel.Text = "Operation failed. Your current image is retained.";
            var detail = exception is OutOfMemoryException
                ? "The image is too large for available memory, or its file data is invalid."
                : exception.Message;
            MessageBox.Show(this, $"The operation could not be completed.\n\n{detail}",
                "Image processing", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            busy = false;
            UseWaitCursor = false;
            UpdateControls();
        }
    }

    private void UpdateControls()
    {
        openButton.Enabled = !busy;
        var canEdit = document is not null && !busy;
        invertButton.Enabled = grayscaleButton.Enabled = resetButton.Enabled =
            fitButton.Enabled = saveButton.Enabled = canEdit;
        clearButton.Enabled = canEdit && document!.Annotations.Count > 0;
        canvas.Enabled = !busy;
        Text = document is null ? ApplicationTitle :
            $"{Path.GetFileName(document.FilePath)}{(document.HasUnsavedChanges ? " *" : "")} — {ApplicationTitle}";
        UpdateZoom();
    }

    private void UpdateZoom() => zoomLabel.Text = document is null ? "" : $"  Zoom: {canvas.Zoom * 100:F2}%";

    private bool ConfirmDiscard() => document?.HasUnsavedChanges != true ||
        MessageBox.Show(this, "Discard unsaved image changes and annotations?", "Unsaved changes",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;

    protected override async void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Control || busy) return;
        if (e.KeyCode is not (Keys.O or Keys.S or Keys.D0 or Keys.NumPad0)) return;
        e.SuppressKeyPress = true;
        if (e.KeyCode == Keys.O) await OpenImageAsync();
        else if (e.KeyCode == Keys.S) await SaveImageAsync();
        else canvas.FitImage();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (busy)
        {
            e.Cancel = true;
            messageLabel.Text = "Please wait for the current operation to finish before closing.";
        }
        else if (!ConfirmDiscard()) e.Cancel = true;
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            canvas.Document = null;
            document?.Dispose();
            document = null;
        }
        base.Dispose(disposing);
        if (disposing) interfaceFont.Dispose();
    }
}
