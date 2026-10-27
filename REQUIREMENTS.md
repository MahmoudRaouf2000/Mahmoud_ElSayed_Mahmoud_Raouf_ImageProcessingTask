# Assessment requirement checklist

All requested implementation items are complete. Checked items below describe implementation coverage; verification evidence and remaining manual checks are in [VERIFICATION.md](VERIFICATION.md).

## 1. Technology

- [x] C#, .NET 8, Windows Forms: `PixralHealth.csproj` targets `net8.0-windows`.
- [x] System.Drawing/GDI+ rendering, with no third-party libraries or package references.
- [x] Visual Studio 2022-compatible `.sln` and SDK-style `.csproj` files.

## 2A. Loading

- [x] Browse local files with `OpenFileDialog`: `MainForm.OpenImageAsync`.
- [x] Decode into an independent `Bitmap`: `ImageFiles.Load`, `ImageProcessor.CopyToArgb`.
- [x] Paint event of a custom image control: `ImageCanvas.RenderCanvas`.
- [x] No dependency on `PictureBox.SizeMode`; no `PictureBox` is used.
- [x] Width, height, creation timestamp, and filename: main window status/title.
- [x] Fit and center on initial load: `ImageCanvas.Document` calls `FitImage`.
- [x] Preserve aspect ratio: one scale factor in `ViewportTransform`.
- [x] Invalid, unsupported, missing, inaccessible, and oversized input errors are caught and displayed; the previous document remains available.
- [x] Dialog cancellation returns without mutation.

## 2B. Pan and zoom

- [x] Left-button press/drag/release pans continuously in screen pixels.
- [x] Middle-button vertical drag zooms continuously using an exponential scale.
- [x] Zoom anchors the current image center, including after pan.
- [x] Forward/inverse mathematical transforms use one shared model.
- [x] Minimum/maximum limits clamp scale to 0.0001–64.
- [x] Double buffering, real-time invalidation, captured drags, and initial-drag zoom calculation prevent flicker and accumulated jumps.
- [x] Losing capture or pressing Escape cancels a drag safely.

## 2C. Filters

- [x] Manual inversion implements `255 - R/G/B`.
- [x] Manual grayscale implements weighted `0.299R + 0.587G + 0.114B`.
- [x] Actual pixel memory processed through `LockBits`; no built-in filter substitution.
- [x] Pixel formats normalize to 32-bit straight-alpha ARGB; loops respect signed stride.
- [x] Alpha remains untouched.
- [x] Current image repaints on filter completion.
- [x] Original colors can be restored from a separate immutable source bitmap.
- [x] Multiple filters compose on current pixels in application order.
- [x] Filter failures dispose the working copy and retain the current image.

## 2D. Annotations

- [x] Right press chooses start, right drag updates live preview, right release commits a persistent line.
- [x] Endpoints and stroke widths are stored in original image coordinates: `Annotation`.
- [x] The same graphics transform renders image and lines, maintaining alignment during pan/zoom.
- [x] Screen-to-image conversion is performed before storing points.
- [x] Annotation endpoints and stroke rendering are constrained to image bounds.
- [x] Committed lines are included in saved output; unfinished previews are not.

## 2E. Saving

- [x] Destination and filename chosen with `SaveFileDialog`.
- [x] PNG, JPEG (`.jpg`/`.jpeg`), and BMP encoders.
- [x] Output includes current filters and all committed lines: `ImageDocument.RenderOutput`.
- [x] Original pixel resolution retained; viewport size, position, and zoom are ignored by export.
- [x] Encoding writes to a temporary file, flushes, checks decoded dimensions, and replaces the destination only after successful encoding.
- [x] Existing source files can be overwritten because loading releases the input stream.
- [x] Success dialog and actionable error dialog; PNG retains alpha and JPEG/BMP use a documented white background.

## 3. Interface

- [x] Top toolbar: Open, Invert, Grayscale, Reset Filters, Save; additional Fit Image and Clear Lines.
- [x] Large custom central canvas, automatically resized with the window.
- [x] Bottom status: dimensions, creation time, zoom, optional pointer coordinates, operation status.
- [x] Consistent font/spacing and DPI-aware startup.
- [x] Double-buffered rendering.
- [x] Image commands disabled without an image; editing disabled during background operations.
- [x] Mouse instructions and tooltips; keyboard shortcuts for open/save/fit.

## 4. Architecture and resources

- [x] Entry point, form, canvas, processor, and annotation model in separate source files.
- [x] Document ownership, file operations, and transformation math isolated into focused helpers.
- [x] UI-independent image processing and file encoding.
- [x] Descriptive names, focused methods, and comments explaining coordinates, alpha, and ownership.
- [x] Deterministic bitmap/stream/graphics/pen/brush/font disposal.
- [x] Safe replacement and disposal during repeated loads and filters; no source file lock after load.
- [x] Exception handling for file, decoder, allocation, processing, and export failures.
- [x] No placeholder methods, TODO implementations, mocks, or external submissions.

## 5. Critical failure modes

- [x] Lines cannot shift independently from pixels: shared image-space transform.
- [x] Conversion formulas are inverse operations: tested round trip.
- [x] Image center stays fixed during zoom: model and mouse-handler checks.
- [x] Pan speed stays consistent across zoom levels: screen-pixel deltas.
- [x] Export includes annotations at original dimensions: reopened output checks.
- [x] Reset uses untouched source pixels and retains annotations.
- [x] Previous bitmaps are disposed after successful replacement.
- [x] Double buffering and capture cancellation prevent stale drag previews.
- [x] Invalid files, cancelled dialogs, and failed saves retain existing document data.

## 6. Verification

- [x] Dependencies restored and entire solution built in Release with zero warnings/errors.
- [x] Pixel values, alpha, filter composition, formats, and signed stride tested.
- [x] Left/middle/right handlers, line preview/finalization, capture loss, Escape, pan consistency, and center anchoring exercised programmatically.
- [x] PNG/JPEG/BMP outputs reopened to check dimensions, processed pixels, and annotations.
- [x] File detachment, overwrite, failed replacement, temporary cleanup, and repeated load/dispose paths exercised.
- [x] Main window instantiated, laid out, and painted; rendered screenshots inspected.
- [x] Remaining physical GUI checks explicitly identified without claiming they were performed.

## 7–8. Deliverables and working phases

- [x] Complete solution, projects, all C# source, and `.gitignore` created.
- [x] README covers overview, requirements, Visual Studio and CLI startup, features, controls, math, filters, saving, structure, and limitations.
- [x] Requirement analysis and structure established; application implemented; build/debug completed; requirements reviewed; documentation delivered.
- [x] Final results include implemented features, this checklist, build/test results, run instructions, and limitations.
- [x] No email or automatic assessment submission performed.
