# Image Processing Task for Applicants

A complete C# .NET 8 Windows Forms application for opening, inspecting, filtering, annotating, and saving images. Rendering uses a double-buffered custom GDI+ canvas and its `Paint` event; no `PictureBox` is used. The application has no third-party package dependencies.

## Requirements and running

- Windows 10 or Windows 11 with a desktop session.
- Visual Studio 2022 17.8 or later with **.NET desktop development** and the .NET 8 SDK/targeting pack. A newer SDK requires a compatible Visual Studio version.
- Running a built application requires the .NET 8 **Desktop Runtime**.

Open `PixralHealth.sln` in Visual Studio, select **PixralHealth** as the startup project, then press **F5**. The verification project is a console executable, not the application startup project.

From PowerShell in the solution directory:

```powershell
dotnet restore PixralHealth.sln
dotnet build PixralHealth.sln --configuration Release --no-restore
dotnet run --project src/PixralHealth --configuration Release --no-build
```

After building, the application can also be launched using `src/PixralHealth/bin/Release/net8.0-windows/PixralHealth.exe`.

## Workflow and controls

1. Choose **Open Image** or **Ctrl+O**. The image fits and centers automatically, preserving its aspect ratio. The status bar shows pixel dimensions, filesystem creation time, zoom, and pointer coordinates.
2. Hold **left mouse** and drag to pan. Movement is measured in screen pixels, so drag speed is consistent at every zoom.
3. Hold **middle mouse** and drag upward to zoom in or downward to zoom out. Zoom stays anchored to the image's current center, including after panning.
4. Hold **right mouse** inside the image and drag to preview a red line. Release to commit it. Endpoints are constrained to the image. A click without movement creates no line.
5. Choose **Invert** or **Grayscale** to process the current colors. Filters compose in the order selected. Invert twice restores the preceding colors; applying grayscale repeatedly is stable.
6. Choose **Reset Filters** to restore original colors while retaining lines. **Clear Lines** removes all annotations.
7. Choose **Fit Image** or **Ctrl+0** to fit and recenter the viewport. **Esc** cancels the current drag. Losing mouse capture also cancels an unfinished line.
8. Choose **Save Image** or **Ctrl+S**, then select PNG, JPEG, or BMP and a destination. A success dialog confirms the path and original pixel dimensions.

The canvas resizes with the window. Before manual pan or zoom, resizing refits the image; afterward it retains the selected scale and moves the image center by half the viewport size change. Image commands are disabled until an image is loaded. Long-running load, filter, and encoding work disables editing to prevent concurrent mutation. Unsaved edits are marked with an asterisk and prompt before opening another image or closing. Cancelling a file dialog leaves the document unchanged.

## Project structure

```text
PixralHealth.sln
src/PixralHealth/
  PixralHealth.csproj       .NET 8 Windows Forms executable
  Program.cs               STA entry point, DPI and application setup
  MainForm.cs              Toolbar, dialogs, status, operation/error handling
  ImageCanvas.cs           Paint rendering, mouse gestures, live line preview
  ViewportTransform.cs     Fit, pan, scale, forward/inverse coordinates
  ImageDocument.cs         Bitmap ownership, original reset, annotations, export composition
  ImageProcessor.cs        ARGB normalization and LockBits pixel algorithms
  Annotation.cs            Immutable line endpoints, color, and image-space width
  ImageFiles.cs            Detached decoding, metadata, checked file export
tests/PixralHealth.Verification/
  PixralHealth.Verification.csproj
  Program.cs               Dependency-free Windows verification executable
REQUIREMENTS.md             Assessment requirement mapping
VERIFICATION.md             Executed checks and remaining manual checks
```

## Transformations

Let `p` be an image coordinate, `I` the original image size, `C` the image center in screen coordinates, and `s` the scale:

```text
screen = C + (p - I / 2) * s
image  = (screen - C) / s + I / 2
```

Pan changes `C` by the mouse displacement in screen pixels. Middle-button zoom uses `startScale * exp((startY - currentY) * 0.01)` and changes only `s`. Therefore the screen position of the image center remains fixed. Computing from the initial drag position avoids accumulated rounding error. Scale is clamped between `0.0001` and `64` (0.01%–6400%); the low minimum permits fitting very large images.

The canvas builds a GDI+ matrix from the same scale and top-left translation. The matrix renders both bitmap pixels and line annotations. Mouse positions are converted by the inverse equation before being stored. Annotation width is three original image pixels, so it scales with the image. Graphics clipping keeps strokes inside the image.

## Pixel processing and ownership

All inputs are normalized to `Format32bppArgb`, including indexed, 24-bit, and premultiplied formats. Straight-alpha ARGB data is copied row by row into independent bitmap storage without changing hidden RGB or translucent values. Other formats are converted through GDI+ with `SourceCopy` compositing.

Filters use `LockBits` with signed row stride and BGRA byte order. They change only RGB:

- **Invert:** each channel becomes `255 - channel`.
- **Grayscale:** `0.299R + 0.587G + 0.114B`, rounded to the nearest integer, with half values rounded upward.
- Alpha remains unchanged by both filters.

The document owns two separate bitmaps: the original and the current filtered image. A filter processes an independent working copy on a background task; only a completed result replaces the displayed bitmap. Reset copies from the untouched original. Filters apply to image colors; line annotations are drawn afterward and retain their red color.

Every decoded image, stream, working bitmap, graphics context, pen, brush, and owned font has an explicit disposal path. Loading detaches the pixels before closing the input stream, so the original file can be renamed or overwritten. Opening a new image disposes the previous document after the replacement has loaded successfully. Closing is blocked while an operation is running; close the window again after it finishes.

## Saving

Export creates a fresh bitmap at the original pixel dimensions, copies current processed pixels, then draws all committed annotations with an identity viewport transform. Pan and zoom never affect output dimensions or annotation coordinates. Valid source DPI is retained.

- **PNG:** retains alpha, including partial transparency.
- **JPEG:** lossy output with transparency composited against white.
- **BMP:** opaque output with transparency composited against white for decoder compatibility.

The filename extension determines the encoder (`.png`, `.jpg`, `.jpeg`, `.bmp`); unsupported extensions produce an error. Output is encoded into a unique temporary file in the destination directory, flushed, reopened to check dimensions, then moved over the destination. Encoding errors occur before replacing the destination, and temporary output is cleaned up on failure. This reduces the risk of damaged existing files but does not guarantee recovery from power loss or network filesystem failures.

## Verification

```powershell
dotnet run --project tests/PixralHealth.Verification --configuration Release
```

The runner exits with a nonzero status if a check fails. It uses real `System.Drawing` bitmaps and WinForms controls on an STA thread, generates temporary image fixtures, and removes those fixtures afterward. It also writes `artifacts/main-window.png` and `artifacts/annotated-canvas.png` for visual inspection; these generated artifacts are excluded from source control.

The Release solution build completed with **zero warnings and errors**, and **10 verification groups passed** on Windows. See [VERIFICATION.md](VERIFICATION.md) for evidence and the distinction between automated control-level checks and manual interaction. See [REQUIREMENTS.md](REQUIREMENTS.md) for the full assessment mapping.

## Limitations

- Windows only; the application uses Windows Forms and GDI+.
- Images must fit in memory. The original, displayed image, and temporary processing/export copies require several full-resolution buffers. Memory and decoder errors are reported without replacing the existing document.
- Input support depends on installed GDI+ codecs (normally PNG, JPEG, BMP, GIF, TIFF). Only the first frame/page is loaded; animation and multipage editing are outside scope.
- EXIF orientation is not automatically applied. The decoded pixel orientation and dimensions are used. Export preserves pixel dimensions and valid DPI, but does not copy EXIF, ICC, or other metadata.
- Lines have a fixed red color and three-image-pixel width. There is no per-line editing or undo history; **Clear Lines** removes them all.
- Saved annotations are flattened into the image; there is no separate editable project format.
- Native file dialog interaction, physical mouse input, multiple monitors, and different display scaling settings still need the manual checks listed in `VERIFICATION.md`.
