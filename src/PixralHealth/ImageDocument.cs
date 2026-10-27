using System.Collections.ObjectModel;
using System.Drawing.Drawing2D;

namespace PixralHealth;

/// <summary>Owns the immutable original and the current filtered image.</summary>
public sealed class ImageDocument : IDisposable
{
    private readonly Bitmap original;
    private readonly List<Annotation> annotations = [];
    private bool disposed;

    public ImageDocument(Bitmap ownedOriginal, string filePath, DateTime creationTime)
    {
        // Ownership transfers only after construction succeeds.
        CurrentImage = ImageProcessor.CopyToArgb(ownedOriginal);
        original = ownedOriginal;
        FilePath = filePath;
        CreationTime = creationTime;
        Annotations = annotations.AsReadOnly();
    }

    public Bitmap CurrentImage { get; private set; }
    public string FilePath { get; }
    public DateTime CreationTime { get; }
    public ReadOnlyCollection<Annotation> Annotations { get; }
    public bool HasUnsavedChanges { get; private set; }

    public void ReplaceProcessedImage(Bitmap ownedImage)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (ownedImage.Size != original.Size || ownedImage.PixelFormat != original.PixelFormat)
            throw new ArgumentException("Processed image must retain dimensions and ARGB format.", nameof(ownedImage));
        if (ReferenceEquals(ownedImage, CurrentImage)) return;
        var previous = CurrentImage;
        CurrentImage = ownedImage;
        previous.Dispose();
        HasUnsavedChanges = true;
    }

    public void ResetFilters() => ReplaceProcessedImage(ImageProcessor.CopyToArgb(original));

    public void AddAnnotation(Annotation annotation)
    {
        annotations.Add(annotation);
        HasUnsavedChanges = true;
    }

    public void ClearAnnotations()
    {
        if (annotations.Count == 0) return;
        annotations.Clear();
        HasUnsavedChanges = true;
    }

    public void MarkSaved() => HasUnsavedChanges = false;

    public Bitmap RenderOutput()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var output = ImageProcessor.CopyToArgb(CurrentImage);
        try
        {
            using var graphics = Graphics.FromImage(output);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.SetClip(new Rectangle(0, 0, output.Width, output.Height));
            foreach (var annotation in annotations) annotation.Draw(graphics);
            return output;
        }
        catch
        {
            output.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        CurrentImage.Dispose();
        original.Dispose();
    }
}
