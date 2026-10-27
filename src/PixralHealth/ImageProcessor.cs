using System.Drawing.Imaging;

namespace PixralHealth;

public enum ImageFilter { Invert, Grayscale }

public static class ImageProcessor
{
    public static Bitmap CopyToArgb(Image source)
    {
        var result = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        try
        {
            if (float.IsFinite(source.HorizontalResolution) && source.HorizontalResolution > 0 &&
                float.IsFinite(source.VerticalResolution) && source.VerticalResolution > 0)
            {
                result.SetResolution(source.HorizontalResolution, source.VerticalResolution);
            }

            // Copy straight-alpha pixels exactly and fully detach stream-backed bitmaps.
            // GDI+ drawing can round translucent RGB; independent storage also avoids decoder dependencies.
            if (source is Bitmap bitmap && bitmap.PixelFormat == PixelFormat.Format32bppArgb)
                CopyArgbPixels(bitmap, result);
            else
            {
                using var graphics = Graphics.FromImage(result);
                graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                graphics.DrawImage(source, new Rectangle(0, 0, result.Width, result.Height),
                    0, 0, source.Width, source.Height, GraphicsUnit.Pixel);
            }
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    private static unsafe void CopyArgbPixels(Bitmap source, Bitmap destination)
    {
        var bounds = new Rectangle(0, 0, source.Width, source.Height);
        var input = source.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var output = destination.LockBits(bounds, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                var rowBytes = (long)source.Width * 4;
                for (var y = 0; y < source.Height; y++)
                    Buffer.MemoryCopy((byte*)input.Scan0 + (nint)y * input.Stride,
                        (byte*)output.Scan0 + (nint)y * output.Stride, rowBytes, rowBytes);
            }
            finally { destination.UnlockBits(output); }
        }
        finally { source.UnlockBits(input); }
    }

    /// <summary>Mutates an owned, straight-alpha ARGB bitmap; alpha is untouched.</summary>
    public static unsafe void ApplyInPlace(Bitmap bitmap, ImageFilter filter)
    {
        if (bitmap.PixelFormat != PixelFormat.Format32bppArgb)
            throw new ArgumentException("Processing requires a 32-bit ARGB bitmap.", nameof(bitmap));
        if (!Enum.IsDefined(filter))
            throw new ArgumentOutOfRangeException(nameof(filter));

        var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            for (var y = 0; y < bitmap.Height; y++)
            {
                // Stride is signed: Scan0 is the first logical row even for bottom-up data.
                var row = (byte*)data.Scan0 + (nint)y * data.Stride;
                for (var x = 0; x < bitmap.Width; x++)
                {
                    var pixel = row + x * 4; // GDI+ stores straight-alpha ARGB as BGRA bytes.
                    if (filter == ImageFilter.Invert)
                    {
                        pixel[0] = (byte)(255 - pixel[0]);
                        pixel[1] = (byte)(255 - pixel[1]);
                        pixel[2] = (byte)(255 - pixel[2]);
                    }
                    else
                    {
                        var gray = (byte)Math.Clamp((int)Math.Round(
                            0.299 * pixel[2] + 0.587 * pixel[1] + 0.114 * pixel[0],
                            MidpointRounding.AwayFromZero), 0, 255);
                        pixel[0] = pixel[1] = pixel[2] = gray;
                    }
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }
}
