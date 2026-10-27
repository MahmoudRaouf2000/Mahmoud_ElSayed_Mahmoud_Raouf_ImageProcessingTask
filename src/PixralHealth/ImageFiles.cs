using System.Drawing.Imaging;

namespace PixralHealth;

public static class ImageFiles
{
    public static ImageDocument Load(string path)
    {
        // Detach all pixel data before closing the stream, so the input file is never held open.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var decoded = Image.FromStream(stream, useEmbeddedColorManagement: true, validateImageData: true);
        var original = ImageProcessor.CopyToArgb(decoded);
        try
        {
            return new ImageDocument(original, Path.GetFullPath(path), File.GetCreationTime(path));
        }
        catch
        {
            original.Dispose();
            throw;
        }
    }

    public static void Save(Bitmap output, string path)
    {
        var format = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => ImageFormat.Png,
            ".jpg" or ".jpeg" => ImageFormat.Jpeg,
            ".bmp" => ImageFormat.Bmp,
            _ => throw new ArgumentException("Use a .png, .jpg, .jpeg, or .bmp filename.", nameof(path))
        };
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)!;
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                if (format.Guid == ImageFormat.Jpeg.Guid || format.Guid == ImageFormat.Bmp.Guid)
                {
                    // JPEG/BMP do not reliably support alpha: composite against white explicitly.
                    using var flattened = new Bitmap(output.Width, output.Height, PixelFormat.Format24bppRgb);
                    flattened.SetResolution(output.HorizontalResolution, output.VerticalResolution);
                    using (var graphics = Graphics.FromImage(flattened))
                    {
                        graphics.Clear(Color.White);
                        graphics.DrawImageUnscaled(output, 0, 0);
                    }
                    flattened.Save(stream, format);
                }
                else output.Save(stream, format);
                stream.Flush(flushToDisk: true);
            }

            // Validate encoding before replacing an existing destination.
            using (var check = Image.FromFile(temporaryPath))
            {
                if (check.Size != output.Size)
                    throw new IOException("The encoded image dimensions do not match the original.");
            }
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                try { File.Delete(temporaryPath); }
                catch (IOException) { /* Do not mask the original save error. */ }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
