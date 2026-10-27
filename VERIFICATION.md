# Verification results

Verification date: 2026-10-27. Environment: Windows 10 x64, .NET SDK 9.0.306, .NET 8 Windows Desktop runtime 8.0.21. Target: `net8.0-windows`.

## Executed commands

```powershell
dotnet restore PixralHealth.sln
dotnet build PixralHealth.sln --configuration Release --no-restore
dotnet run --project tests/PixralHealth.Verification --configuration Release --no-build
```

Result: restore succeeded; Release build succeeded with **0 warnings and 0 errors**; **10 verification groups passed, 0 failed**. The verification executable returns exit code 0 on success.

## Automated evidence

| Group | Behaviors checked |
| --- | --- |
| Invert | Every pixel across multiple rows; exact RGB formula; unchanged partial/zero alpha; double inversion; exact translucent/hidden RGB copying |
| Grayscale | Red/green/blue reference luminances; rounding; partial alpha; repeated grayscale and subsequent inversion |
| Formats | Odd-width 24-bit input; indexed PNG palette decoding; normalization to ARGB; premultiplied-alpha conversion |
| Signed stride | Externally allocated negative-stride bitmap processed and all output pixels checked |
| Reset and document | Original restoration after filtering; retained annotations; dirty/saved state; clearing lines |
| Transform | Aspect-preserving fit; coordinate round trip; screen-pixel pan; fixed center after zoom/pan; min/max scale; NaN rejection |
| Export | Nonempty PNG/JPG/JPEG/BMP files reopened; original dimensions; filtered background and line pixels; PNG transparency; white background for opaque formats |
| Files and errors | Exclusive access after load; overwrite original path; 30 load/render/save/dispose cycles; invalid image rejection; unsupported extension; locked destination error; existing destination retained; temporary output cleanup |
| Canvas | Real WinForms control hosted on an STA thread; right-drag preview paint and commit; stored image coordinates; left pan at two zoom levels; middle zoom centered after pan; annotation rendering after transforms; export independent of viewport; Escape/capture-loss cancellation; resize and fit |
| Main window | Real form creation, layout and paint; image toolbar buttons disabled initially; usable canvas size; screenshot generation |

Canvas tests call the protected mouse/key event handlers through a test subclass and exercise actual GDI+ paint output using `DrawToBitmap`. They do not generate physical hardware input. Forms are hosted outside the visible desktop area during verification. Screenshots in `artifacts/` provide render evidence; these are not physical screen captures. The main-window screenshot was visually inspected during implementation.

Resource disposal is covered by explicit ownership review and repeated operations, not by a long-running memory profiler. Visual Studio itself was not launched; the solution was restored and built using the .NET CLI.

## Manual acceptance checks still to perform

These checks require a person interacting with the application and are **not claimed as executed**:

1. Open the app with F5 in Visual Studio. Confirm Open/Save native dialog keyboard navigation and cancellation, then choose a real local image and inspect displayed metadata.
2. Use a physical three-button mouse: left drag should pan, middle vertical drag should zoom around the image center, and right drag should preview/commit a line. Drag beyond canvas edges and release; check capture behavior.
3. Apply several filters, reset, and compare with the original; inspect alpha visually against the checkerboard.
4. Save each format through the actual dialog, including selecting a different format, choosing a filename extension, overwriting a file, and declining overwrite. Open the outputs in another viewer.
5. Exercise error/success message dialogs, unsaved-change prompts, and close attempts during processing. Check readability with long filenames and timestamps.
6. Test at 100%, 150%, and 200% display scaling and move between monitors. Resize and maximize with loaded images. Check toolbar overflow and status layout.
7. Load large images and repeat editing sessions while monitoring private memory and GDI handles if a performance or soak assessment is required.

The implementation includes all requested behaviors; the distinction above records which behavior was executed automatically and which still requires physical interaction or a broader environment matrix.
