# Sprite Slicer Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Ein zweiter Tab „Slice" in Simple Image Scaler, der ein Sprite-Sheet per einstellbarem Raster (Anzahl oder Zellgrösse, 4 Ränder, Abstände) in einzelne PNG-Zellen zerschneidet, mit manuell ziehbaren Schnittlinien und Drag-out der Ergebnisse.

**Architecture:** Die Rechenlogik liegt UI-frei in `ImageScaler.Core` und ist voll testbar: `GridGenerator` erzeugt aus den Reglern **Bänder** (Spalte = `(Start,End)` in Bildpixeln), `SliceGrid` hält Bänder + Zellen und bietet die manuellen Operationen (splitten, löschen, Kante verschieben), `ImageSlicer` schneidet mit SkiaSharp PNG-Bytes aus. Das WPF-UI zeichnet das Raster auf ein `Canvas` über der Vorschau und ruft nur diese Core-API auf. Bänder statt reiner Linien, weil nur so Lücken (Abstand) darstellbar sind.

**Tech Stack:** C# / .NET 11, WPF, SkiaSharp (Encoding/Subset), xUnit.

---

## Hinweise für die Umsetzung

- Bestehender Code, den du kennen musst:
  - `ImageScaler.Core`: `JpegCompressor`, `TempSession` (`Create()`, `FolderPath`, `Dispose()`), `CompressionOptions/Result`.
  - `src/ImageScaler/MainWindow.xaml(.cs)`: aktuelle 3-Spalten-Compress-UI, `ObservableCollection<ImageItem> Inputs/Outputs`, Drag-out via `DataObject(DataFormats.FileDrop, paths)`.
  - `src/ImageScaler/Loc.cs`: Lokalisierung, `Loc.Instance["Key"]`, XAML bindet `{Binding [Key], Source={x:Static local:Loc.Instance}}`. **Jeder neue UI-Text muss in beide Wörterbücher (`en`, `de`).**
  - `src/ImageScaler/ImageItem.cs`: Listeneintrag (FilePath, FileName, SizeBytes, StatusText, …) — wird für die Slice-Ergebnisse wiederverwendet.
  - `src/ImageScaler/PathToThumbnailConverter.cs`: Thumbnails, lädt mit `BitmapCacheOption.OnLoad` (Datei bleibt entsperrt).
- TDD gilt für `ImageScaler.Core` (Tasks 1–4). Das WPF-UI (Tasks 5–10) wird manuell verifiziert, wie im bestehenden Projekt.
- Alle Koordinaten in der Core-Logik sind **Bildpixel**, nie Canvas-Pixel.
- Commit-Messages enden mit einer Leerzeile und dann:
  `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`

---

## Task 1: Core — Datentypen für das Raster

**Files:**
- Create: `src/ImageScaler.Core/Band.cs`
- Create: `src/ImageScaler.Core/CellRect.cs`
- Create: `src/ImageScaler.Core/GridParams.cs`

**Step 1: Band.cs**

```csharp
namespace ImageScaler.Core;

/// <summary>Ein Spalten- oder Zeilen-Band in Bildpixeln. End ist exklusiv.</summary>
public readonly record struct Band(int Start, int End)
{
    public int Length => End - Start;
}
```

**Step 2: CellRect.cs**

```csharp
namespace ImageScaler.Core;

/// <summary>Eine Zelle des Rasters: Position im Raster (Row/Col) + Rechteck in Bildpixeln.</summary>
public readonly record struct CellRect(int Row, int Col, int X, int Y, int Width, int Height);
```

**Step 3: GridParams.cs**

```csharp
namespace ImageScaler.Core;

public enum GridMode { ByCount, BySize }

/// <summary>Die Regler-Werte, aus denen das Raster erzeugt wird. Alle Masse in Bildpixeln.</summary>
public sealed class GridParams
{
    public GridMode Mode { get; init; } = GridMode.ByCount;

    // Mode == ByCount
    public int Columns { get; init; } = 4;
    public int Rows { get; init; } = 4;

    // Mode == BySize
    public int CellWidth { get; init; } = 32;
    public int CellHeight { get; init; } = 32;

    public int MarginLeft { get; init; }
    public int MarginTop { get; init; }
    public int MarginRight { get; init; }
    public int MarginBottom { get; init; }

    public int SpacingX { get; init; }
    public int SpacingY { get; init; }
}
```

**Step 4: Build**

Run: `dotnet build src/ImageScaler.Core`
Expected: PASS

**Step 5: Commit**

```bash
git add src/ImageScaler.Core/Band.cs src/ImageScaler.Core/CellRect.cs src/ImageScaler.Core/GridParams.cs
git commit -m "feat(core): add grid types for sprite slicer"
```

---

## Task 2: Core — `SliceGrid` mit Zellen

**Files:**
- Create: `src/ImageScaler.Core/SliceGrid.cs`
- Test: `tests/ImageScaler.Tests/SliceGridTests.cs`

**Step 1: Test schreiben**

```csharp
using ImageScaler.Core;
using Xunit;

namespace ImageScaler.Tests;

public class SliceGridTests
{
    [Fact]
    public void Cells_AreCrossProductOfColumnsAndRows()
    {
        var grid = new SliceGrid();
        grid.Columns.AddRange(new[] { new Band(0, 10), new Band(10, 20) });
        grid.Rows.AddRange(new[] { new Band(0, 5) });

        var cells = grid.Cells.ToList();

        Assert.Equal(2, cells.Count);
        Assert.Contains(cells, c => c is { Row: 0, Col: 0, X: 0, Y: 0, Width: 10, Height: 5 });
        Assert.Contains(cells, c => c is { Row: 0, Col: 1, X: 10, Y: 0, Width: 10, Height: 5 });
    }
}
```

**Step 2: Test ausführen — muss fehlschlagen**

Run: `dotnet test --filter SliceGrid`
Expected: FAIL (SliceGrid existiert nicht)

**Step 3: Implementierung**

```csharp
namespace ImageScaler.Core;

/// <summary>
/// Das Raster als Spalten- und Zeilen-Bänder. Zellen sind das Kreuzprodukt.
/// Bänder (statt reiner Schnittlinien), damit Abstände zwischen Zellen abbildbar sind.
/// </summary>
public sealed class SliceGrid
{
    public List<Band> Columns { get; } = new();
    public List<Band> Rows { get; } = new();

    public IEnumerable<CellRect> Cells
    {
        get
        {
            for (int r = 0; r < Rows.Count; r++)
                for (int c = 0; c < Columns.Count; c++)
                    yield return new CellRect(
                        r, c,
                        Columns[c].Start, Rows[r].Start,
                        Columns[c].Length, Rows[r].Length);
        }
    }
}
```

**Step 4: Test ausführen**

Run: `dotnet test --filter SliceGrid`
Expected: PASS

**Step 5: Commit**

```bash
git add -A
git commit -m "feat(core): add SliceGrid with cell cross-product"
```

---

## Task 3: Core — `GridGenerator` (Anzahl-Modus, Ränder)

**Files:**
- Create: `src/ImageScaler.Core/GridGenerator.cs`
- Test: `tests/ImageScaler.Tests/GridGeneratorTests.cs`

**Step 1: Tests schreiben**

```csharp
using ImageScaler.Core;
using Xunit;

namespace ImageScaler.Tests;

public class GridGeneratorTests
{
    [Fact]
    public void ByCount_ProducesExactCellCountAndSize()
    {
        var grid = GridGenerator.Generate(400, 300,
            new GridParams { Mode = GridMode.ByCount, Columns = 4, Rows = 3 });

        Assert.Equal(4, grid.Columns.Count);
        Assert.Equal(3, grid.Rows.Count);
        Assert.Equal(12, grid.Cells.Count());
        Assert.All(grid.Columns, b => Assert.Equal(100, b.Length));
        Assert.All(grid.Rows, b => Assert.Equal(100, b.Length));
        Assert.Equal(new Band(0, 100), grid.Columns[0]);
        Assert.Equal(new Band(300, 400), grid.Columns[3]);
    }

    [Fact]
    public void Margins_ShiftAndShrinkTheGridArea()
    {
        // Bereich: x 10..90 (80 breit), y 20..80 (60 hoch)
        var grid = GridGenerator.Generate(100, 100, new GridParams
        {
            Mode = GridMode.ByCount, Columns = 2, Rows = 1,
            MarginLeft = 10, MarginRight = 10, MarginTop = 20, MarginBottom = 20
        });

        Assert.Equal(new Band(10, 50), grid.Columns[0]);
        Assert.Equal(new Band(50, 90), grid.Columns[1]);
        Assert.Equal(new Band(20, 80), grid.Rows[0]);
    }
}
```

**Step 2: Test ausführen — muss fehlschlagen**

Run: `dotnet test --filter GridGenerator`
Expected: FAIL (GridGenerator existiert nicht)

**Step 3: Implementierung**

```csharp
namespace ImageScaler.Core;

/// <summary>Erzeugt aus den Regler-Werten (GridParams) die Bänder des Rasters.</summary>
public static class GridGenerator
{
    public static SliceGrid Generate(int imageWidth, int imageHeight, GridParams p)
    {
        var grid = new SliceGrid();

        int areaX = p.MarginLeft;
        int areaY = p.MarginTop;
        int areaW = imageWidth - p.MarginLeft - p.MarginRight;
        int areaH = imageHeight - p.MarginTop - p.MarginBottom;
        if (areaW <= 0 || areaH <= 0) return grid;   // Ränder fressen das Bild -> leeres Raster

        grid.Columns.AddRange(BuildBands(areaX, areaW, p.SpacingX,
            p.Mode, p.Columns, p.CellWidth));
        grid.Rows.AddRange(BuildBands(areaY, areaH, p.SpacingY,
            p.Mode, p.Rows, p.CellHeight));

        return grid;
    }

    private static List<Band> BuildBands(int areaStart, int areaLength, int spacing,
        GridMode mode, int count, int cellSize)
    {
        var bands = new List<Band>();
        if (spacing < 0) spacing = 0;

        int n, size;
        if (mode == GridMode.ByCount)
        {
            n = count;
            if (n <= 0) return bands;
            size = (areaLength - spacing * (n - 1)) / n;
        }
        else
        {
            size = cellSize;
            if (size <= 0) return bands;
            n = (areaLength + spacing) / (size + spacing);
        }
        if (size <= 0 || n <= 0) return bands;

        for (int i = 0; i < n; i++)
        {
            int start = areaStart + i * (size + spacing);
            int end = start + size;
            if (end > areaStart + areaLength) break;   // nie über den Bereich hinaus
            bands.Add(new Band(start, end));
        }
        return bands;
    }
}
```

**Step 4: Tests ausführen**

Run: `dotnet test --filter GridGenerator`
Expected: PASS

**Step 5: Commit**

```bash
git add -A
git commit -m "feat(core): add GridGenerator for count mode and margins"
```

---

## Task 4: Core — `GridGenerator` Zellgrössen-Modus + Abstände

**Files:**
- Modify: `tests/ImageScaler.Tests/GridGeneratorTests.cs`

**Step 1: Tests ergänzen**

```csharp
[Fact]
public void BySize_DerivesCountFromCellSize()
{
    var grid = GridGenerator.Generate(128, 96,
        new GridParams { Mode = GridMode.BySize, CellWidth = 32, CellHeight = 32 });

    Assert.Equal(4, grid.Columns.Count);
    Assert.Equal(3, grid.Rows.Count);
    Assert.Equal(new Band(0, 32), grid.Columns[0]);
    Assert.Equal(new Band(96, 128), grid.Columns[3]);
}

[Fact]
public void Spacing_CreatesGapsBetweenCells()
{
    // 100 breit, 2 Spalten, 10px Abstand -> Zelle = (100-10)/2 = 45
    var grid = GridGenerator.Generate(100, 10, new GridParams
    {
        Mode = GridMode.ByCount, Columns = 2, Rows = 1, SpacingX = 10
    });

    Assert.Equal(new Band(0, 45), grid.Columns[0]);
    Assert.Equal(new Band(55, 100), grid.Columns[1]);
}

[Fact]
public void BySize_WithSpacing_StopsBeforeOverflowing()
{
    // 100 breit, Zelle 30, Abstand 10 -> passt 2x (0..30, 40..70); ein drittes (80..110) wäre zu breit
    var grid = GridGenerator.Generate(100, 30, new GridParams
    {
        Mode = GridMode.BySize, CellWidth = 30, CellHeight = 30, SpacingX = 10
    });

    Assert.Equal(2, grid.Columns.Count);
    Assert.Equal(new Band(0, 30), grid.Columns[0]);
    Assert.Equal(new Band(40, 70), grid.Columns[1]);
}

[Fact]
public void AllCells_StayInsideImageBounds()
{
    var grid = GridGenerator.Generate(333, 177, new GridParams
    {
        Mode = GridMode.ByCount, Columns = 7, Rows = 5,
        MarginLeft = 3, MarginTop = 4, MarginRight = 5, MarginBottom = 6, SpacingX = 2, SpacingY = 3
    });

    Assert.NotEmpty(grid.Cells);
    Assert.All(grid.Cells, c =>
    {
        Assert.True(c.X >= 0 && c.Y >= 0);
        Assert.True(c.X + c.Width <= 333, $"cell right {c.X + c.Width} > 333");
        Assert.True(c.Y + c.Height <= 177, $"cell bottom {c.Y + c.Height} > 177");
        Assert.True(c.Width > 0 && c.Height > 0);
    });
}
```

**Step 2: Tests ausführen**

Run: `dotnet test --filter GridGenerator`
Expected: PASS (die Logik aus Task 3 deckt das ab). Falls `BySize_WithSpacing_StopsBeforeOverflowing` fehlschlägt, prüfe die `break`-Bedingung in `BuildBands`.

**Step 3: Commit**

```bash
git add -A
git commit -m "test(core): cover size mode, spacing and bounds"
```

---

## Task 5: Core — manuelles Bearbeiten der Linien

**Files:**
- Modify: `src/ImageScaler.Core/SliceGrid.cs`
- Modify: `tests/ImageScaler.Tests/SliceGridTests.cs`

**Kontext:** Das UI braucht drei Operationen: nächstgelegene Kante finden (zum Greifen), Kante verschieben (Ziehen), Linie einfügen (Doppelklick) und entfernen (Rechtsklick). Bei Abstand 0 liegen zwei Kanten aufeinander — die müssen sich gemeinsam bewegen, damit es sich wie **eine** Linie anfühlt.

**Step 1: Tests schreiben**

```csharp
[Fact]
public void SplitColumnAt_DividesTheContainingBand()
{
    var grid = new SliceGrid();
    grid.Columns.Add(new Band(0, 100));

    grid.SplitColumnAt(40);

    Assert.Equal(2, grid.Columns.Count);
    Assert.Equal(new Band(0, 40), grid.Columns[0]);
    Assert.Equal(new Band(40, 100), grid.Columns[1]);
}

[Fact]
public void SplitColumnAt_IgnoresPositionOutsideAnyBand()
{
    var grid = new SliceGrid();
    grid.Columns.Add(new Band(0, 40));
    grid.Columns.Add(new Band(60, 100));  // Lücke 40..60

    grid.SplitColumnAt(50);   // in der Lücke -> nichts tun

    Assert.Equal(2, grid.Columns.Count);
}

[Fact]
public void FindNearestColumnEdge_ReturnsClosestWithinTolerance()
{
    var grid = new SliceGrid();
    grid.Columns.Add(new Band(0, 40));
    grid.Columns.Add(new Band(40, 100));

    var handle = grid.FindNearestColumnEdge(43, tolerance: 5);

    Assert.NotNull(handle);
    Assert.Equal(40, grid.GetColumnEdge(handle!.Value));
    Assert.Null(grid.FindNearestColumnEdge(70, tolerance: 5));  // zu weit weg
}

[Fact]
public void MoveColumnEdge_MovesCoincidentEdgesTogether()
{
    var grid = new SliceGrid();
    grid.Columns.Add(new Band(0, 40));
    grid.Columns.Add(new Band(40, 100));   // Kanten liegen aufeinander (Abstand 0)

    var handle = grid.FindNearestColumnEdge(40, tolerance: 2)!.Value;
    grid.MoveColumnEdge(handle, 60);

    Assert.Equal(new Band(0, 60), grid.Columns[0]);
    Assert.Equal(new Band(60, 100), grid.Columns[1]);
}

[Fact]
public void MoveColumnEdge_ClampsSoBandKeepsMinimumWidth()
{
    var grid = new SliceGrid();
    grid.Columns.Add(new Band(0, 40));
    grid.Columns.Add(new Band(40, 100));

    var handle = grid.FindNearestColumnEdge(40, tolerance: 2)!.Value;
    grid.MoveColumnEdge(handle, -50);   // weit über den Anfang hinaus

    Assert.True(grid.Columns[0].Length >= 1);
    Assert.True(grid.Columns[1].Length >= 1);
}

[Fact]
public void RemoveColumnEdge_MergesTheTwoBands()
{
    var grid = new SliceGrid();
    grid.Columns.Add(new Band(0, 40));
    grid.Columns.Add(new Band(40, 100));

    var handle = grid.FindNearestColumnEdge(40, tolerance: 2)!.Value;
    grid.RemoveColumnEdge(handle);

    Assert.Single(grid.Columns);
    Assert.Equal(new Band(0, 100), grid.Columns[0]);
}
```

**Step 2: Tests ausführen — müssen fehlschlagen**

Run: `dotnet test --filter SliceGrid`
Expected: FAIL (Methoden fehlen)

**Step 3: Implementierung (an SliceGrid anhängen)**

```csharp
/// <summary>Zeigt auf eine Kante eines Bandes: welches Band, und ob dessen Start oder Ende.</summary>
public readonly record struct EdgeHandle(int BandIndex, bool IsStart);

public sealed class SliceGrid
{
    // ... Columns, Rows, Cells wie in Task 2 ...

    private const int MinBandLength = 1;

    // ---- Spalten ----
    public void SplitColumnAt(int x) => SplitAt(Columns, x);
    public EdgeHandle? FindNearestColumnEdge(int x, int tolerance) => FindNearestEdge(Columns, x, tolerance);
    public int GetColumnEdge(EdgeHandle h) => GetEdge(Columns, h);
    public void MoveColumnEdge(EdgeHandle h, int newPos) => MoveEdge(Columns, h, newPos);
    public void RemoveColumnEdge(EdgeHandle h) => RemoveEdge(Columns, h);

    // ---- Zeilen ----
    public void SplitRowAt(int y) => SplitAt(Rows, y);
    public EdgeHandle? FindNearestRowEdge(int y, int tolerance) => FindNearestEdge(Rows, y, tolerance);
    public int GetRowEdge(EdgeHandle h) => GetEdge(Rows, h);
    public void MoveRowEdge(EdgeHandle h, int newPos) => MoveEdge(Rows, h, newPos);
    public void RemoveRowEdge(EdgeHandle h) => RemoveEdge(Rows, h);

    // ---- gemeinsame Logik ----
    private static void SplitAt(List<Band> bands, int pos)
    {
        for (int i = 0; i < bands.Count; i++)
        {
            var b = bands[i];
            if (pos <= b.Start + MinBandLength || pos >= b.End - MinBandLength) continue;
            bands[i] = new Band(b.Start, pos);
            bands.Insert(i + 1, new Band(pos, b.End));
            return;
        }
    }

    private static EdgeHandle? FindNearestEdge(List<Band> bands, int pos, int tolerance)
    {
        EdgeHandle? best = null;
        int bestDist = int.MaxValue;
        for (int i = 0; i < bands.Count; i++)
        {
            foreach (var isStart in new[] { true, false })
            {
                int edge = isStart ? bands[i].Start : bands[i].End;
                int dist = Math.Abs(edge - pos);
                if (dist <= tolerance && dist < bestDist)
                {
                    bestDist = dist;
                    best = new EdgeHandle(i, isStart);
                }
            }
        }
        return best;
    }

    private static int GetEdge(List<Band> bands, EdgeHandle h)
        => h.IsStart ? bands[h.BandIndex].Start : bands[h.BandIndex].End;

    private static void MoveEdge(List<Band> bands, EdgeHandle h, int newPos)
    {
        int oldPos = GetEdge(bands, h);
        // Alle Kanten, die exakt auf der alten Position liegen, wandern mit (Abstand 0 = eine Linie).
        var affected = new List<EdgeHandle>();
        for (int i = 0; i < bands.Count; i++)
        {
            if (bands[i].Start == oldPos) affected.Add(new EdgeHandle(i, true));
            if (bands[i].End == oldPos) affected.Add(new EdgeHandle(i, false));
        }

        int clamped = ClampEdge(bands, affected, newPos);
        foreach (var a in affected)
        {
            var b = bands[a.BandIndex];
            bands[a.BandIndex] = a.IsStart ? new Band(clamped, b.End) : new Band(b.Start, clamped);
        }
    }

    /// <summary>Begrenzt die neue Position so, dass alle betroffenen Bänder gültig bleiben.</summary>
    private static int ClampEdge(List<Band> bands, List<EdgeHandle> affected, int newPos)
    {
        int min = int.MinValue, max = int.MaxValue;
        foreach (var a in affected)
        {
            var b = bands[a.BandIndex];
            if (a.IsStart) max = Math.Min(max, b.End - MinBandLength);
            else min = Math.Max(min, b.Start + MinBandLength);
        }
        if (min != int.MinValue) newPos = Math.Max(newPos, min);
        if (max != int.MaxValue) newPos = Math.Min(newPos, max);
        return newPos;
    }

    private static void RemoveEdge(List<Band> bands, EdgeHandle h)
    {
        int i = h.BandIndex;
        // Verschmelze das Band mit dem Nachbarn auf der Seite der Kante.
        int a = h.IsStart ? i - 1 : i;
        int b = a + 1;
        if (a < 0 || b >= bands.Count) return;
        bands[a] = new Band(bands[a].Start, bands[b].End);
        bands.RemoveAt(b);
    }
}
```

**Step 4: Tests ausführen**

Run: `dotnet test --filter SliceGrid`
Expected: PASS

**Step 5: Commit**

```bash
git add -A
git commit -m "feat(core): add manual line editing to SliceGrid"
```

---

## Task 6: Core — `ImageSlicer`

**Files:**
- Create: `src/ImageScaler.Core/ImageSlicer.cs`
- Test: `tests/ImageScaler.Tests/ImageSlicerTests.cs`

**Step 1: Tests schreiben**

Der wichtige Test prüft nicht nur die Anzahl, sondern dass die **richtigen Pixel** in der richtigen Zelle landen: ein Bild mit 4 verschiedenfarbigen Quadranten wird 2×2 geschnitten, jede Zelle muss ihre Farbe haben.

```csharp
using ImageScaler.Core;
using SkiaSharp;
using Xunit;

namespace ImageScaler.Tests;

public class ImageSlicerTests
{
    private static readonly SKColor TopLeft = new(255, 0, 0);
    private static readonly SKColor TopRight = new(0, 255, 0);
    private static readonly SKColor BottomLeft = new(0, 0, 255);
    private static readonly SKColor BottomRight = new(255, 255, 0);

    /// <summary>64x64 PNG mit vier einfarbigen Quadranten.</summary>
    private static string CreateQuadrantImage()
    {
        using var bmp = new SKBitmap(64, 64);
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
                bmp.SetPixel(x, y, (x < 32, y < 32) switch
                {
                    (true, true) => TopLeft,
                    (false, true) => TopRight,
                    (true, false) => BottomLeft,
                    _ => BottomRight
                });

        var path = Path.Combine(Path.GetTempPath(), $"quad_{Guid.NewGuid():N}.png");
        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        using var fs = File.OpenWrite(path);
        data.SaveTo(fs);
        return path;
    }

    [Fact]
    public void Slice_ProducesOnePngPerCell_WithCorrectPixels()
    {
        var path = CreateQuadrantImage();
        var grid = GridGenerator.Generate(64, 64,
            new GridParams { Mode = GridMode.ByCount, Columns = 2, Rows = 2 });

        var results = ImageSlicer.Slice(path, grid);

        Assert.Equal(4, results.Count);
        foreach (var r in results)
        {
            using var cell = SKBitmap.Decode(r.PngData);
            Assert.Equal(32, cell.Width);
            Assert.Equal(32, cell.Height);

            var expected = (r.Col, r.Row) switch
            {
                (0, 0) => TopLeft,
                (1, 0) => TopRight,
                (0, 1) => BottomLeft,
                _ => BottomRight
            };
            // Mitte der Zelle prüfen
            Assert.Equal(expected, cell.GetPixel(16, 16));
        }
        File.Delete(path);
    }

    [Fact]
    public void Slice_PreservesTransparency()
    {
        using var bmp = new SKBitmap(2, 2);
        bmp.SetPixel(0, 0, SKColors.Transparent);
        bmp.SetPixel(1, 0, SKColors.Transparent);
        bmp.SetPixel(0, 1, SKColors.Transparent);
        bmp.SetPixel(1, 1, SKColors.Transparent);
        var path = Path.Combine(Path.GetTempPath(), $"trans_{Guid.NewGuid():N}.png");
        using (var img = SKImage.FromBitmap(bmp))
        using (var data = img.Encode(SKEncodedImageFormat.Png, 100))
        using (var fs = File.OpenWrite(path))
            data.SaveTo(fs);

        var grid = GridGenerator.Generate(2, 2,
            new GridParams { Mode = GridMode.ByCount, Columns = 1, Rows = 1 });
        var results = ImageSlicer.Slice(path, grid);

        using var cell = SKBitmap.Decode(results[0].PngData);
        Assert.Equal(0, cell.GetPixel(0, 0).Alpha);
        File.Delete(path);
    }

    [Fact]
    public void Slice_ThrowsOnUnreadableImage()
    {
        var path = Path.Combine(Path.GetTempPath(), $"broken_{Guid.NewGuid():N}.png");
        File.WriteAllText(path, "not an image");
        var grid = new SliceGrid();

        Assert.Throws<InvalidOperationException>(() => ImageSlicer.Slice(path, grid));
        File.Delete(path);
    }
}
```

**Step 2: Tests ausführen — müssen fehlschlagen**

Run: `dotnet test --filter ImageSlicer`
Expected: FAIL (ImageSlicer existiert nicht)

**Step 3: Implementierung**

```csharp
using SkiaSharp;

namespace ImageScaler.Core;

/// <summary>Ergebnis einer geschnittenen Zelle: Rasterposition + fertige PNG-Bytes.</summary>
public sealed class SliceResult
{
    public required int Row { get; init; }
    public required int Col { get; init; }
    public required byte[] PngData { get; init; }
    public long Size => PngData.LongLength;
}

/// <summary>Schneidet die Zellen eines SliceGrid als PNG aus einem Bild.</summary>
public static class ImageSlicer
{
    public static List<SliceResult> Slice(string inputPath, SliceGrid grid)
    {
        using var bitmap = SKBitmap.Decode(inputPath)
            ?? throw new InvalidOperationException($"Bild konnte nicht gelesen werden: {inputPath}");
        using var image = SKImage.FromBitmap(bitmap);

        var results = new List<SliceResult>();
        foreach (var cell in grid.Cells)
        {
            var rect = SKRectI.Create(cell.X, cell.Y, cell.Width, cell.Height);
            using var subset = image.Subset(rect);
            if (subset == null) continue;   // Zelle liegt ausserhalb -> überspringen
            using var data = subset.Encode(SKEncodedImageFormat.Png, 100);
            results.Add(new SliceResult { Row = cell.Row, Col = cell.Col, PngData = data.ToArray() });
        }
        return results;
    }
}
```

**Step 4: Tests ausführen**

Run: `dotnet test`
Expected: alle grün (bestehende + neue)

**Step 5: Commit**

```bash
git add -A
git commit -m "feat(core): add ImageSlicer producing PNG cells"
```

---

## Task 7: WPF — Tabs einführen (Compress / Slice)

**Files:**
- Modify: `src/ImageScaler/MainWindow.xaml`
- Modify: `src/ImageScaler/Loc.cs`

**Ziel:** Das bestehende 3-Spalten-Grid wandert unverändert in einen `TabItem` „Compress"; ein zweiter leerer `TabItem` „Slice" kommt dazu. **Kein Verhalten am Compress-Tab ändern.**

**Step 1: Loc-Keys ergänzen** (in beide Wörterbücher `en` und `de`)

```
["TabCompress"]  = "Compress"          / "Komprimieren"
["TabSlice"]     = "Slice"             / "Schneiden"
```

**Step 2: XAML umbauen**

- Das äussere `<Grid Margin="16">` mit den drei Spalten in ein `<TabControl>` einbetten:

```xml
<TabControl Background="Transparent" BorderThickness="0" Margin="8">
    <TabItem Header="{Binding [TabCompress], Source={x:Static local:Loc.Instance}}">
        <!-- HIER: das bestehende 3-Spalten-Grid unverändert einfügen -->
    </TabItem>
    <TabItem Header="{Binding [TabSlice], Source={x:Static local:Loc.Instance}}">
        <!-- Task 8 füllt das -->
        <Grid/>
    </TabItem>
</TabControl>
```

**Step 3: Build & manuelle Prüfung**

Run: `dotnet build` und `dotnet run --project src/ImageScaler`
Expected: Zwei Tabs sichtbar; „Compress" funktioniert exakt wie vorher (Bilder reinziehen, komprimieren, rausziehen); „Slice" ist leer.

**Step 4: Commit**

```bash
git add -A
git commit -m "feat(ui): move compress view into tabs, add empty slice tab"
```

---

## Task 8: WPF — Slice-Tab Layout + Bild reinziehen + Vorschau

**Files:**
- Create: `src/ImageScaler/SliceView.xaml`
- Create: `src/ImageScaler/SliceView.xaml.cs`
- Modify: `src/ImageScaler/MainWindow.xaml` (SliceView in den Slice-Tab einsetzen: `<local:SliceView/>`)
- Modify: `src/ImageScaler/Loc.cs`

**Ziel:** Eigene `UserControl`, damit `MainWindow` nicht weiter aufbläht.

**Step 1: Loc-Keys ergänzen** (en / de)

```
["SliceDropHint"] = "Drag a sprite sheet here"      / "Sprite-Sheet hierher ziehen"
["GridMode"]      = "Grid mode"                     / "Raster-Modus"
["ByCount"]       = "By count"                      / "Nach Anzahl"
["BySize"]        = "By cell size"                  / "Nach Zellgrösse"
["ColumnsRows"]   = "Columns × Rows"                / "Spalten × Zeilen"
["CellSize"]      = "Cell size (px)"                / "Zellgrösse (px)"
["Margins"]       = "Margins (px)"                  / "Ränder (px)"
["MarginLeft"]    = "Left"                          / "Links"
["MarginTop"]     = "Top"                           / "Oben"
["MarginRight"]   = "Right"                         / "Rechts"
["MarginBottom"]  = "Bottom"                        / "Unten"
["Spacing"]       = "Spacing (px)"                  / "Abstand (px)"
["SliceButton"]   = "Slice"                         / "Schneiden"
["SliceHelp"]     = "Drag lines to move · Double-click to add · Right-click to remove"
                  / "Linie ziehen zum Verschieben · Doppelklick zum Hinzufügen · Rechtsklick zum Entfernen"
["SliceOutputHint"] = "Cut cells appear here"       / "Geschnittene Zellen erscheinen hier"
["NoSheet"]       = "No image loaded."              / "Kein Bild geladen."
["SlicedCells"]   = "{0} cells"                     / "{0} Zellen"
```

**Step 2: SliceView.xaml — Layout**

Drei Spalten (`*`, `300`, `*`), im Stil des Compress-Tabs (gleiche Farben/Styles; die Styles aus `MainWindow.xaml` nach `App.xaml` in `<Application.Resources>` verschieben, damit beide Views sie nutzen — dabei die Compress-Ansicht nicht kaputt machen).

- **Links:** `Border` mit `AllowDrop="True"`, darin ein `Grid`:
  - `<Image x:Name="Preview" Stretch="Uniform"/>`
  - `<Canvas x:Name="Overlay" Background="Transparent"/>` (darüber; zeichnet die Linien)
  - `<TextBlock x:Name="DropHint" Text="{Binding [SliceDropHint], ...}"/>` (mittig, wenn kein Bild)
- **Mitte:** `ComboBox` Modus, `TextBox`en für Spalten/Zeilen bzw. Zellgrösse (je nach Modus ein/ausblenden), 4 `TextBox`en für Ränder, 2 für Abstand, `Button` „Schneiden", `TextBlock` mit `SliceHelp`, Status-`TextBlock`.
- **Rechts:** `ListBox x:Name="OutputList"` mit `SelectionMode="Extended"` + `DropHint` — identisch zum Compress-Tab (gleiches `ItemTpl`/`CardList`, gleiche Drag-out-Handler).

**Step 3: SliceView.xaml.cs — Bild laden & Vorschau**

```csharp
private string? _sheetPath;
private int _imgW, _imgH;

private void Root_DragOver(object sender, DragEventArgs e)
{
    e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
    e.Handled = true;
}

private void Root_Drop(object sender, DragEventArgs e)
{
    if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
    var path = ((string[])e.Data.GetData(DataFormats.FileDrop))
        .FirstOrDefault(p => File.Exists(p) &&
            new[] { ".jpg", ".jpeg", ".png", ".bmp", ".webp", ".gif" }
                .Contains(Path.GetExtension(p).ToLowerInvariant()));
    if (path == null) return;
    LoadSheet(path);
}

private void LoadSheet(string path)
{
    var bmp = new BitmapImage();
    bmp.BeginInit();
    bmp.CacheOption = BitmapCacheOption.OnLoad;   // Datei nicht sperren
    bmp.UriSource = new Uri(path);
    bmp.EndInit();
    bmp.Freeze();

    _sheetPath = path;
    _imgW = bmp.PixelWidth;
    _imgH = bmp.PixelHeight;
    Preview.Source = bmp;
    DropHint.Visibility = Visibility.Collapsed;
    RebuildGrid();     // Task 9
}
```

**Step 4: Manuelle Prüfung**

App starten, Slice-Tab öffnen, ein Bild reinziehen.
Expected: Bild erscheint eingepasst in der linken Spalte, Hinweistext verschwindet. Regler sind sichtbar (noch ohne Wirkung).

**Step 5: Commit**

```bash
git add -A
git commit -m "feat(ui): add slice tab layout with sheet preview"
```

---

## Task 9: WPF — Raster zeichnen + Regler verdrahten

**Files:**
- Modify: `src/ImageScaler/SliceView.xaml.cs`

**Kernstück:** die Abbildung Bildpixel ⇄ Canvas-Pixel. Das `Image` nutzt `Stretch="Uniform"`, also:

```csharp
private double _scale, _offX, _offY;

/// <summary>Berechnet, wie das Bild ins Canvas eingepasst wird (Uniform + zentriert).</summary>
private void UpdateTransform()
{
    if (_imgW == 0 || _imgH == 0) return;
    double cw = Overlay.ActualWidth, ch = Overlay.ActualHeight;
    _scale = Math.Min(cw / _imgW, ch / _imgH);
    _offX = (cw - _imgW * _scale) / 2;
    _offY = (ch - _imgH * _scale) / 2;
}

private double ToCanvasX(int ix) => _offX + ix * _scale;
private double ToCanvasY(int iy) => _offY + iy * _scale;
private int ToImageX(double cx) => (int)Math.Round((cx - _offX) / _scale);
private int ToImageY(double cy) => (int)Math.Round((cy - _offY) / _scale);
```

**Step 1: Raster erzeugen und zeichnen**

```csharp
private SliceGrid _grid = new();

private GridParams ReadParams() => new()
{
    Mode = ModeBox.SelectedIndex == 1 ? GridMode.BySize : GridMode.ByCount,
    Columns = ParseInt(ColumnsBox.Text, 4),
    Rows = ParseInt(RowsBox.Text, 4),
    CellWidth = ParseInt(CellWBox.Text, 32),
    CellHeight = ParseInt(CellHBox.Text, 32),
    MarginLeft = ParseInt(MarginLeftBox.Text, 0),
    MarginTop = ParseInt(MarginTopBox.Text, 0),
    MarginRight = ParseInt(MarginRightBox.Text, 0),
    MarginBottom = ParseInt(MarginBottomBox.Text, 0),
    SpacingX = ParseInt(SpacingXBox.Text, 0),
    SpacingY = ParseInt(SpacingYBox.Text, 0),
};

private static int ParseInt(string s, int fallback)
    => int.TryParse(s?.Trim(), out var v) && v >= 0 ? v : fallback;

/// <summary>Regler-Änderung: Raster komplett neu bauen (manuelle Linien gehen bewusst verloren).</summary>
private void RebuildGrid()
{
    if (_sheetPath == null) return;
    _grid = GridGenerator.Generate(_imgW, _imgH, ReadParams());
    Redraw();
}

private void Redraw()
{
    Overlay.Children.Clear();
    if (_sheetPath == null) return;
    UpdateTransform();

    // Zellen als Rechtecke zeichnen: zeigt Lücken (Abstand) automatisch als Nicht-Rechteck-Bereiche.
    foreach (var cell in _grid.Cells)
    {
        var r = new Rectangle
        {
            Width = Math.Max(1, cell.Width * _scale),
            Height = Math.Max(1, cell.Height * _scale),
            Stroke = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)),
            StrokeThickness = 1,
            Fill = Brushes.Transparent
        };
        Canvas.SetLeft(r, ToCanvasX(cell.X));
        Canvas.SetTop(r, ToCanvasY(cell.Y));
        Overlay.Children.Add(r);
    }
    StatusLabel.Text = string.Format(Loc.Instance["SlicedCells"], _grid.Cells.Count());
}
```

- Alle Regler-`TextBox`en: `TextChanged="OnParamChanged"`, die `ComboBox`: `SelectionChanged="OnParamChanged"` → beide rufen `RebuildGrid()`.
- `Overlay.SizeChanged += (_, _) => Redraw();` damit das Raster beim Fenster-Resize mitgeht.
- Modus-Umschaltung blendet die Felder um: `ByCount` → Spalten/Zeilen sichtbar; `BySize` → Zellgrösse sichtbar.

**Step 2: Manuelle Prüfung**

Bild laden, Regler drehen.
Expected: Raster erscheint sofort und passt sich live an; Ränder verschieben den Bereich; Abstand erzeugt sichtbare Lücken; Zellenzahl im Status stimmt; Fenster-Resize verzerrt nichts.

**Step 3: Commit**

```bash
git add -A
git commit -m "feat(ui): draw grid overlay and wire slice controls"
```

---

## Task 10: WPF — Linien ziehen / hinzufügen / entfernen

**Files:**
- Modify: `src/ImageScaler/SliceView.xaml.cs`

**Step 1: Maus-Handler am `Overlay`**

Toleranz zum Greifen: 6 Canvas-Pixel → in Bildpixel umrechnen (`(int)Math.Ceiling(6 / _scale)`, mindestens 1).

```csharp
private EdgeHandle? _dragHandle;
private bool _dragIsColumn;

private void Overlay_MouseMove(object sender, MouseEventArgs e)
{
    if (_sheetPath == null) return;
    var p = e.GetPosition(Overlay);
    int ix = ToImageX(p.X), iy = ToImageY(p.Y);
    int tol = Math.Max(1, (int)Math.Ceiling(6 / _scale));

    if (e.LeftButton == MouseButtonState.Pressed && _dragHandle != null)
    {
        if (_dragIsColumn) _grid.MoveColumnEdge(_dragHandle.Value, ix);
        else _grid.MoveRowEdge(_dragHandle.Value, iy);
        Redraw();
        return;
    }

    // Cursor-Feedback, wenn eine Linie in der Nähe ist
    bool nearCol = _grid.FindNearestColumnEdge(ix, tol) != null;
    bool nearRow = _grid.FindNearestRowEdge(iy, tol) != null;
    Overlay.Cursor = nearCol ? Cursors.SizeWE : nearRow ? Cursors.SizeNS : Cursors.Arrow;
}

private void Overlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
{
    if (_sheetPath == null) return;
    var p = e.GetPosition(Overlay);
    int ix = ToImageX(p.X), iy = ToImageY(p.Y);
    int tol = Math.Max(1, (int)Math.Ceiling(6 / _scale));

    var col = _grid.FindNearestColumnEdge(ix, tol);
    if (col != null) { _dragHandle = col; _dragIsColumn = true; Overlay.CaptureMouse(); return; }

    var row = _grid.FindNearestRowEdge(iy, tol);
    if (row != null) { _dragHandle = row; _dragIsColumn = false; Overlay.CaptureMouse(); }
}

private void Overlay_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
{
    _dragHandle = null;
    Overlay.ReleaseMouseCapture();
}

/// <summary>Doppelklick fügt eine Linie ein – vertikal oder horizontal, je nachdem was näher liegt.</summary>
private void Overlay_MouseDoubleClick(object sender, MouseButtonEventArgs e)
{
    if (_sheetPath == null) return;
    var p = e.GetPosition(Overlay);
    int ix = ToImageX(p.X), iy = ToImageY(p.Y);

    // Entscheide anhand der Zellform: in der breiteren Richtung teilen wirkt natürlicher.
    if (e.ChangedButton == MouseButton.Left)
    {
        if (Keyboard.Modifiers == ModifierKeys.Shift) _grid.SplitRowAt(iy);
        else _grid.SplitColumnAt(ix);
        Redraw();
    }
}

private void Overlay_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
{
    if (_sheetPath == null) return;
    var p = e.GetPosition(Overlay);
    int ix = ToImageX(p.X), iy = ToImageY(p.Y);
    int tol = Math.Max(1, (int)Math.Ceiling(6 / _scale));

    var col = _grid.FindNearestColumnEdge(ix, tol);
    if (col != null) { _grid.RemoveColumnEdge(col.Value); Redraw(); return; }
    var row = _grid.FindNearestRowEdge(iy, tol);
    if (row != null) { _grid.RemoveRowEdge(row.Value); Redraw(); }
}
```

- Hinweis: Doppelklick = **vertikale** Linie, **Shift+Doppelklick** = horizontale Linie. Das gehört in `SliceHelp` (Loc-Text entsprechend ergänzen: „Double-click adds a vertical line, Shift+double-click a horizontal one" / „Doppelklick setzt eine vertikale Linie, Shift+Doppelklick eine waagrechte").

**Step 2: Manuelle Prüfung**

Expected: Cursor wechselt über Linien zu ↔ / ↕; Ziehen verschiebt die Linie live (bei Abstand 0 bewegt sich die gemeinsame Kante als eine Linie); Doppelklick setzt eine neue Linie; Rechtsklick entfernt sie; Regler drehen baut alles neu.

**Step 3: Commit**

```bash
git add -A
git commit -m "feat(ui): drag, add and remove slice lines"
```

---

## Task 11: WPF — Schneiden + Ausgabe + Drag-out

**Files:**
- Modify: `src/ImageScaler/SliceView.xaml.cs`

**Step 1: Schneiden-Handler**

```csharp
public ObservableCollection<ImageItem> Outputs { get; } = new();

private async void SliceButton_Click(object sender, RoutedEventArgs e)
{
    if (_sheetPath == null) { StatusLabel.Text = Loc.Instance["NoSheet"]; return; }
    var session = ((App)Application.Current).Session;
    if (session == null) { StatusLabel.Text = Loc.Instance["NoSession"]; return; }

    SliceButton.IsEnabled = false;
    Outputs.Clear();
    var path = _sheetPath;
    var grid = _grid;

    try
    {
        var results = await Task.Run(() => ImageSlicer.Slice(path, grid));
        var baseName = Path.GetFileNameWithoutExtension(path);
        foreach (var r in results)
        {
            var outPath = Path.Combine(session.FolderPath, $"{baseName}_r{r.Row}_c{r.Col}.png");
            await File.WriteAllBytesAsync(outPath, r.PngData);
            Outputs.Add(new ImageItem
            {
                FilePath = outPath,
                FileName = Path.GetFileName(outPath),
                SizeBytes = r.Size,
                StatusText = ""
            });
        }
        StatusLabel.Text = string.Format(Loc.Instance["SlicedCells"], results.Count);
    }
    catch (Exception ex)
    {
        StatusLabel.Text = Loc.Instance["ErrorPrefix"] + ex.Message;
    }
    finally
    {
        SliceButton.IsEnabled = true;
    }
}
```

- Namenskollisionen sind hier ausgeschlossen (Row/Col sind eindeutig), aber ein zweiter Schnitt desselben Sheets überschreibt bewusst die alten Dateien.

**Step 2: Drag-out**

Die Handler `OutputList_PreviewMouseLeftButtonDown` / `OutputList_MouseMove` **1:1 aus `MainWindow.xaml.cs` übernehmen** (gleiche Logik: Drag-Schwelle, `SelectedItems` → `DataObject(DataFormats.FileDrop, paths)` → `DragDrop.DoDragDrop`).

**Step 3: Manuelle Prüfung**

Sheet laden, Raster einstellen, „Schneiden".
Expected: Rechts erscheint pro Zelle ein PNG mit Thumbnail und Grösse; einzeln und mehrfach in einen Explorer-Ordner ziehbar; App schliessen → Temp-Ordner ist leer.

**Step 4: Commit**

```bash
git add -A
git commit -m "feat(ui): slice cells to temp png files with drag-out"
```

---

## Task 12: Abschluss — README, Screenshot, Version

**Files:**
- Modify: `README.md`
- Modify: `src/ImageScaler/ImageScaler.csproj` (Version auf `0.1.0`)
- Modify: `docs/images/` (neuer Screenshot des Slice-Tabs)

**Step 1: Version**

`<Version>0.1.0</Version>` (neues Feature → Minor-Bump).

**Step 2: README ergänzen**

- Feature-Liste: „✂️ **Sprite slicer** – cut a sheet into a grid of PNG cells: set columns/rows or cell size, adjust margins and spacing, drag individual cut lines, then drag the cells out."
- Ein „Usage"-Unterabschnitt für den Slice-Tab (Bild reinziehen → Regler → Linien anpassen → Schneiden → rausziehen).
- Screenshot des Slice-Tabs einfügen.

**Step 3: Verifikation**

Run: `dotnet test` → alle grün. `dotnet build` → 0 Fehler.

**Step 4: Commit**

```bash
git add -A
git commit -m "docs: document sprite slicer, bump to 0.1.0"
```

---

## Definition of Done

- `dotnet test` grün (Core-Logik: Generator, manuelles Editieren, Slicer inkl. Pixel-Korrektheit).
- Compress-Tab funktioniert unverändert wie vorher.
- Slice-Tab: Sheet reinziehen → Raster über Regler (Anzahl **und** Zellgrösse, 4 Ränder, Abstände) → Linien von Hand ziehen/hinzufügen/entfernen → „Schneiden" → PNG-Zellen rechts → rausziehbar.
- Alle neuen Texte sind auf Englisch **und** Deutsch vorhanden.
- Temp-Zellen werden beim Schliessen gelöscht.
