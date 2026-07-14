# Simple Image Scaler

A lightweight Windows tool with two jobs:

- **Compress** images down to a target file size (e.g. 10 MB) — for getting photos under an upload limit (Discord, email, the web) in seconds.
- **Slice** a sprite sheet into a grid of individual PNG cells.

Both work the same way: drag your image in on the left, set things up in the middle, drag the finished files out on the right. The results are temporary and are cleaned up automatically when you close the app.

Available in **English and German** (English by default — switch any time in the settings).

![Simple Image Scaler](docs/images/screenshot.png)

> ⚠️ **Windows only.** Simple Image Scaler is a WPF app and runs on Windows (x64) only.

---

## Features

- 🎯 **Target size per image** – fixed presets (8 / 10 / 25 / 50 MB) or your own value in MB
- 🔍 **Best quality for the size** – resolution is preserved; only the JPEG quality is tuned via binary search to the highest value that still fits under the target
- ✂️ **Sprite slicer** – cut a sheet into a grid of PNG cells: set columns/rows or cell size, adjust margins and spacing, drag individual cut lines, then drag the cells out
- 🖱️ **Drag & drop in and out** – drag images in, drag the finished ones straight into Discord, a folder, or an email
- 🌍 **Bilingual UI** – English and German, switchable at runtime
- 🧹 **Automatic cleanup** – results are temporary and deleted on exit (orphaned files from a crash are removed on the next start)
- 🟠 **Clear feedback** – each image shows its quality and new size; targets that can't be met are marked orange, errors red
- 📦 **No installation** – a single `.exe` with the runtime bundled in

---

## Usage — Compress

1. **Drag images in** – Drag one or more images from Explorer into the **left** column ("Original"). Supported: `.jpg`, `.jpeg`, `.png`, `.bmp`, `.webp`, `.gif`. Other files are ignored.
2. **Pick a target size** – In the **middle**, choose a preset (8 / 10 / 25 / 50 MB), or select `Custom…` and type a value in MB.
3. **Compress** – Click **Compress**. The progress bar runs and the results appear on the right as JPEGs with their new size and quality info.
4. **Drag out** – In the **right** column, select one or more images and drag them wherever you need them (a Discord chat, a folder, an email …).
5. **Close** – When you exit, the temporary results are deleted automatically. Anything you already dragged out is kept, of course.

> 💡 The output format is always **JPEG**, because it lets the target size be hit most precisely and opens everywhere. (PNG transparency is lost in the process.)

> 🌐 Use the **Language** dropdown at the top of the settings column to switch between English and German.

---

## Usage — Slice

Switch to the **Slice** tab to cut a sprite sheet into individual cells.

![Sprite slicer](docs/images/slicer.png)

1. **Drag a sprite sheet in** – Drop one image into the **left** panel. It replaces whatever is loaded; the grid is drawn on top of it.
2. **Set up the grid** – In the **middle**:
   - **Grid mode** — `By count` (give columns × rows) or `By cell size` (give the cell size in pixels and the count is worked out for you).
   - **Margins** — left / top / right / bottom. Use these when the sprites don't start at the image edge, so the grid doesn't cut through them.
   - **Spacing** — the gap between cells, if your sheet has separator lines between tiles.
3. **Fine-tune by hand** – Adjust individual cut lines directly on the image:
   - **Drag** a line to move it.
   - **Double-click** adds a vertical line; **Shift+double-click** adds a horizontal one.
   - **Right-click** a line removes it.
4. **Slice** – Click **Slice**. Every cell is written as a **PNG** (lossless, transparency preserved) and appears on the right.
5. **Drag out** – Select one or more cells and drag them into a folder.

> ⚠️ Changing any slider **rebuilds the grid from scratch**, discarding manual line edits. So set the sliders roughly first, then fine-tune by hand.

---

## Download & run

1. Download the latest `Simple Image Scaler.exe` from the [Releases](../../releases).
2. Double-click — that's it. No installation required.

On first launch **Windows SmartScreen** may warn ("Unknown publisher") because the `.exe` is not code-signed. Click **More info → Run anyway** to start it.

---

## Build from source

**Requirements:** [.NET SDK 11](https://dotnet.microsoft.com/download) (or newer), Windows x64.

```bash
# Clone the repository
git clone https://github.com/<your-user>/simple-image-scaler.git
cd simple-image-scaler

# Run the tests
dotnet test

# Run in debug mode
dotnet run --project src/ImageScaler
```

### Produce a standalone .exe

```bash
dotnet publish src/ImageScaler/ImageScaler.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish_out
```

The resulting `Simple Image Scaler.exe` will be in the `publish_out` folder (~68 MB, since the .NET runtime and SkiaSharp are bundled in).

---

## Project structure

```
src/
  ImageScaler.Core/     # UI-free logic: JpegCompressor, GridGenerator, SliceGrid,
                        # ImageSlicer, TempSession
  ImageScaler/          # WPF app (tabs, drag & drop, grid overlay, localization)
tests/
  ImageScaler.Tests/    # Unit tests for the core logic
docs/plans/             # Design and implementation documents
```

---

## How it works

- **[.NET 11](https://dotnet.microsoft.com/) / WPF** – desktop UI
- **[SkiaSharp](https://github.com/mono/SkiaSharp)** (MIT) – image encoding
- **xUnit** – tests

All the real logic lives in `ImageScaler.Core`, free of any UI, so it can be tested directly.

**Compression** loads the image and repeatedly encodes it in memory at different JPEG quality levels (5–95) using a binary search, then picks the highest quality whose file still fits under the target size. If the image is already smaller than the target it is saved at maximum quality; if the target can't be reached even at minimum quality, the image is still produced and flagged.

**Slicing** models the grid as *bands* rather than plain cut lines — a column is a range `(start, end)` in image pixels. That is what makes gaps between cells representable: with spacing at 0 the neighbouring edges coincide and behave like a single line when dragged, and with spacing above 0 the gap simply falls between two bands. Cells are the cross product of the column and row bands, and each one is cut out and encoded as a PNG.

---

## License

Released under the [PolyForm Noncommercial License 1.0.0](LICENSE).

You are free to use, modify, and share this software **for any noncommercial purpose** (personal use, hobby projects, education, charities, etc.). **Commercial use — including selling it — is not permitted** without a separate license from the author. For commercial licensing, please get in touch.

The bundled SkiaSharp library is MIT-licensed.
