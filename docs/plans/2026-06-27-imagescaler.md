# ImageScaler Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Eine WPF-Desktop-App (.NET 11), die per Drag & Drop Bilder auf eine einstellbare Zielgrösse (z.B. 10 MB) als JPEG komprimiert; die temporären Ergebnisse lassen sich nach Discord/Explorer ziehen und werden beim Schliessen gelöscht.

**Architecture:** Drei-Spalten-WPF-UI (Eingang | Einstellungen | Output). Die Komprimierungs-Logik liegt UI-frei in `ImageScaler.Core` (testbar via SkiaSharp + Binärsuche über die JPEG-Qualität). Temp-Ergebnisse leben in einem Session-Ordner unter `%TEMP%`, der beim Beenden gelöscht wird.

**Tech Stack:** C# / .NET 11, WPF (`net11.0-windows`, `UseWPF`), SkiaSharp (MIT) für Bild-Encoding, xUnit für Tests.

---

## Hinweise für die Umsetzung

- TDD nur für `ImageScaler.Core` (reine Logik). WPF-UI wird **manuell** verifiziert (Schritte je Task beschrieben), da UI-Unit-Tests hier mehr Aufwand als Wert bringen — YAGNI.
- SkiaSharp-Encode-Kern:
  ```csharp
  using var bitmap = SKBitmap.Decode(inputPath);
  using var image = SKImage.FromBitmap(bitmap);
  using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality); // quality 0..100
  long bytes = data.Size;        // Grösse prüfen
  data.SaveTo(outputStream);     // oder data.ToArray()
  ```
- Qualitätsgrenzen: `MinQuality = 5`, `MaxQuality = 95`.

---

## Task 0: Solution & Projekt-Gerüst

**Files:**
- Create: `e:\scaler\.gitignore`
- Create: `e:\scaler\ImageScaler.sln`
- Create: `e:\scaler\src\ImageScaler.Core\ImageScaler.Core.csproj`
- Create: `e:\scaler\src\ImageScaler\ImageScaler.csproj`
- Create: `e:\scaler\tests\ImageScaler.Tests\ImageScaler.Tests.csproj`

**Step 1: Git initialisieren**

```bash
cd /e/scaler
git init
```

**Step 2: .gitignore anlegen**

Inhalt (Standard .NET):
```
bin/
obj/
*.user
.vs/
```

**Step 3: Projekte & Solution erstellen**

```bash
cd /e/scaler
dotnet new classlib -n ImageScaler.Core -o src/ImageScaler.Core
dotnet new wpf -n ImageScaler -o src/ImageScaler
dotnet new xunit -n ImageScaler.Tests -o tests/ImageScaler.Tests
dotnet new sln -n ImageScaler
dotnet sln add src/ImageScaler.Core src/ImageScaler tests/ImageScaler.Tests
```

**Step 4: Referenzen & Pakete**

```bash
cd /e/scaler
dotnet add src/ImageScaler.Core package SkiaSharp
dotnet add src/ImageScaler reference src/ImageScaler.Core
dotnet add tests/ImageScaler.Tests reference src/ImageScaler.Core
```

**Step 5: Target Frameworks prüfen/setzen**

- `src/ImageScaler.Core/ImageScaler.Core.csproj` → `<TargetFramework>net11.0</TargetFramework>`
- `src/ImageScaler/ImageScaler.csproj` → `<TargetFramework>net11.0-windows</TargetFramework>`, `<UseWPF>true</UseWPF>`
- `tests/ImageScaler.Tests/ImageScaler.Tests.csproj` → `<TargetFramework>net11.0</TargetFramework>`

**Step 6: Build prüfen**

Run: `dotnet build`
Expected: Build erfolgreich, 3 Projekte.

**Step 7: Commit**

```bash
git add -A
git commit -m "chore: scaffold ImageScaler solution"
```

---

## Task 1: Core — Datentypen

**Files:**
- Create: `src/ImageScaler.Core/CompressionResult.cs`
- Create: `src/ImageScaler.Core/CompressionOptions.cs`

**Step 1: CompressionResult schreiben**

```csharp
namespace ImageScaler.Core;

public sealed class CompressionResult
{
    public required byte[] Data { get; init; }
    public required int Quality { get; init; }
    public long Size => Data.LongLength;
    /// <summary>True, wenn die Zielgrösse erreicht wurde.</summary>
    public required bool TargetMet { get; init; }
}
```

**Step 2: CompressionOptions schreiben**

```csharp
namespace ImageScaler.Core;

public sealed class CompressionOptions
{
    /// <summary>Zielgrösse pro Bild in Bytes.</summary>
    public required long TargetSizeBytes { get; init; }
    public int MinQuality { get; init; } = 5;
    public int MaxQuality { get; init; } = 95;
}
```

**Step 3: Build**

Run: `dotnet build src/ImageScaler.Core`
Expected: PASS

**Step 4: Commit**

```bash
git add -A
git commit -m "feat(core): add compression result and options types"
```

---

## Task 2: Core — `JpegCompressor.CompressToTarget` (Happy Path)

**Files:**
- Create: `src/ImageScaler.Core/JpegCompressor.cs`
- Test: `tests/ImageScaler.Tests/JpegCompressorTests.cs`
- Test-Assets: `tests/ImageScaler.Tests/assets/` (Testbilder generieren, siehe Step 1)

**Step 1: Test-Helper für synthetische Bilder + erster Test**

Wir erzeugen Testbilder programmatisch mit SkiaSharp (kein Binär-Asset nötig). Dazu in den Tests SkiaSharp referenzieren:

```bash
dotnet add tests/ImageScaler.Tests package SkiaSharp
```

```csharp
using ImageScaler.Core;
using SkiaSharp;
using Xunit;

namespace ImageScaler.Tests;

public class JpegCompressorTests
{
    // Erzeugt ein „verrauschtes" Bild, das als JPEG nicht trivial klein wird.
    private static string CreateNoiseImage(int w, int h)
    {
        using var bmp = new SKBitmap(w, h);
        var rnd = new Random(42);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                bmp.SetPixel(x, y, new SKColor(
                    (byte)rnd.Next(256), (byte)rnd.Next(256), (byte)rnd.Next(256)));
        var path = Path.Combine(Path.GetTempPath(), $"noise_{Guid.NewGuid():N}.png");
        using (var img = SKImage.FromBitmap(bmp))
        using (var data = img.Encode(SKEncodedImageFormat.Png, 100))
        using (var fs = File.OpenWrite(path))
            data.SaveTo(fs);
        return path;
    }

    [Fact]
    public void CompressToTarget_ResultUnderTarget_WhenAchievable()
    {
        var path = CreateNoiseImage(1500, 1500);
        var target = 200_000L; // 200 KB
        var result = JpegCompressor.CompressToTarget(path, new CompressionOptions { TargetSizeBytes = target });

        Assert.True(result.TargetMet);
        Assert.True(result.Size <= target, $"Size {result.Size} > target {target}");
        Assert.InRange(result.Quality, 5, 95);
        File.Delete(path);
    }
}
```

**Step 2: Test ausführen — muss fehlschlagen**

Run: `dotnet test --filter CompressToTarget_ResultUnderTarget_WhenAchievable`
Expected: FAIL (JpegCompressor existiert nicht).

**Step 3: Minimale Implementierung (Binärsuche)**

```csharp
using SkiaSharp;

namespace ImageScaler.Core;

public static class JpegCompressor
{
    public static CompressionResult CompressToTarget(string inputPath, CompressionOptions options)
    {
        using var bitmap = SKBitmap.Decode(inputPath)
            ?? throw new InvalidOperationException($"Bild konnte nicht gelesen werden: {inputPath}");
        using var image = SKImage.FromBitmap(bitmap);

        byte[] Encode(int q)
        {
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, q);
            return data.ToArray();
        }

        int lo = options.MinQuality, hi = options.MaxQuality;

        // Passt selbst die Maximalqualität? Dann fertig (auch der „schon klein genug"-Fall).
        var hiData = Encode(hi);
        if (hiData.LongLength <= options.TargetSizeBytes)
            return new CompressionResult { Data = hiData, Quality = hi, TargetMet = true };

        // Binärsuche nach höchster Qualität, die noch passt.
        byte[]? best = null;
        int bestQ = lo;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            var data = Encode(mid);
            if (data.LongLength <= options.TargetSizeBytes)
            {
                best = data; bestQ = mid; lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        if (best is not null)
            return new CompressionResult { Data = best, Quality = bestQ, TargetMet = true };

        // Nicht erreichbar: Minimalqualität ausgeben, markiert.
        var minData = Encode(options.MinQuality);
        return new CompressionResult { Data = minData, Quality = options.MinQuality, TargetMet = false };
    }
}
```

**Step 4: Test ausführen — muss bestehen**

Run: `dotnet test --filter CompressToTarget_ResultUnderTarget_WhenAchievable`
Expected: PASS

**Step 5: Commit**

```bash
git add -A
git commit -m "feat(core): add JpegCompressor.CompressToTarget with binary search"
```

---

## Task 3: Core — Edge Cases (schon klein genug / nicht erreichbar)

**Files:**
- Modify: `tests/ImageScaler.Tests/JpegCompressorTests.cs`

**Step 1: Tests schreiben**

```csharp
[Fact]
public void CompressToTarget_KeepsHighQuality_WhenAlreadySmall()
{
    var path = CreateNoiseImage(200, 200);
    var result = JpegCompressor.CompressToTarget(path,
        new CompressionOptions { TargetSizeBytes = 50_000_000L }); // 50 MB

    Assert.True(result.TargetMet);
    Assert.Equal(95, result.Quality); // Maximalqualität, kein Verschlechtern
    File.Delete(path);
}

[Fact]
public void CompressToTarget_ReturnsImageAndFlag_WhenTargetUnreachable()
{
    var path = CreateNoiseImage(2000, 2000);
    var result = JpegCompressor.CompressToTarget(path,
        new CompressionOptions { TargetSizeBytes = 500L }); // 500 Bytes – unmöglich

    Assert.False(result.TargetMet);
    Assert.True(result.Data.Length > 0);
    Assert.Equal(5, result.Quality); // Minimalqualität
    File.Delete(path);
}
```

**Step 2: Tests ausführen**

Run: `dotnet test`
Expected: Alle PASS (die Logik aus Task 2 deckt beide Fälle bereits ab).

**Step 3: Commit**

```bash
git add -A
git commit -m "test(core): cover already-small and unreachable target cases"
```

---

## Task 4: Core — Session-Temp-Verwaltung

**Files:**
- Create: `src/ImageScaler.Core/TempSession.cs`
- Test: `tests/ImageScaler.Tests/TempSessionTests.cs`

**Step 1: Tests schreiben**

```csharp
using ImageScaler.Core;
using Xunit;

namespace ImageScaler.Tests;

public class TempSessionTests
{
    [Fact]
    public void Create_MakesSessionFolder_UnderRoot()
    {
        using var session = TempSession.Create();
        Assert.True(Directory.Exists(session.FolderPath));
        Assert.StartsWith(TempSession.RootPath, session.FolderPath);
    }

    [Fact]
    public void Dispose_DeletesSessionFolder()
    {
        var session = TempSession.Create();
        var folder = session.FolderPath;
        File.WriteAllText(Path.Combine(folder, "x.txt"), "hi");
        session.Dispose();
        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    public void CleanupOrphans_RemovesOtherSessionFolders()
    {
        var orphan = Path.Combine(TempSession.RootPath, "orphan_test");
        Directory.CreateDirectory(orphan);
        using var session = TempSession.Create();

        TempSession.CleanupOrphans(exceptFolderPath: session.FolderPath);

        Assert.False(Directory.Exists(orphan));
        Assert.True(Directory.Exists(session.FolderPath));
    }
}
```

**Step 2: Tests ausführen — müssen fehlschlagen**

Run: `dotnet test --filter TempSession`
Expected: FAIL (TempSession existiert nicht).

**Step 3: Implementierung**

```csharp
namespace ImageScaler.Core;

public sealed class TempSession : IDisposable
{
    public static string RootPath { get; } =
        Path.Combine(Path.GetTempPath(), "ImageScaler");

    public string FolderPath { get; }

    private TempSession(string folderPath) => FolderPath = folderPath;

    public static TempSession Create()
    {
        var folder = Path.Combine(RootPath, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return new TempSession(folder);
    }

    /// <summary>Löscht alle Session-Ordner ausser dem aktuellen (Absturz-Reste).</summary>
    public static void CleanupOrphans(string? exceptFolderPath = null)
    {
        if (!Directory.Exists(RootPath)) return;
        foreach (var dir in Directory.GetDirectories(RootPath))
        {
            if (exceptFolderPath is not null &&
                string.Equals(Path.GetFullPath(dir), Path.GetFullPath(exceptFolderPath),
                    StringComparison.OrdinalIgnoreCase))
                continue;
            TryDelete(dir);
        }
    }

    public void Dispose() => TryDelete(FolderPath);

    private static void TryDelete(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
        catch { /* gesperrte Dateien ignorieren – beim nächsten Start aufgeräumt */ }
    }
}
```

**Step 4: Tests ausführen — müssen bestehen**

Run: `dotnet test --filter TempSession`
Expected: PASS

**Step 5: Commit**

```bash
git add -A
git commit -m "feat(core): add TempSession for temp file lifecycle"
```

---

## Task 5: WPF — Drei-Spalten-Shell + ViewModel-Grundgerüst

**Files:**
- Modify: `src/ImageScaler/MainWindow.xaml`
- Modify: `src/ImageScaler/MainWindow.xaml.cs`
- Create: `src/ImageScaler/ImageItem.cs`

**Step 1: `ImageItem` (Listeneintrag) anlegen**

```csharp
using System.ComponentModel;

namespace ImageScaler;

public sealed class ImageItem : INotifyPropertyChanged
{
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public required long SizeBytes { get; set; }
    public string SizeDisplay => $"{SizeBytes / 1024.0 / 1024.0:0.00} MB";

    // Nur für Output relevant:
    public int Quality { get; set; }
    public bool TargetMet { get; set; } = true;
    public bool HasError { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
}
```

**Step 2: MainWindow.xaml — 3-Spalten-Grid**

Layout: `Grid` mit 3 Spalten (`*`, `Auto`, `*`), GridSplitter optional. Links `ListBox x:Name="InputList"` (mit `AllowDrop="True"`), Mitte ein `StackPanel` mit:
- `ComboBox x:Name="TargetPreset"` (Items: „8 MB", „10 MB", „25 MB", „50 MB", „Eigene…")
- `TextBox x:Name="CustomMb"` (nur aktiv bei „Eigene…")
- `TextBlock` „Format: JPEG"
- `Button x:Name="CompressButton" Content="Komprimieren"`
- `ProgressBar x:Name="Progress"`
- `Button x:Name="ClearButton" Content="Liste leeren"`

Rechts `ListBox x:Name="OutputList"` (`SelectionMode="Extended"`).

Beide ListBoxen mit einem `ItemTemplate`: Thumbnail (`Image` mit `Source` aus `FilePath`, `DecodePixelWidth=64` via Converter oder direkt gebundenes `BitmapImage`), daneben `FileName` + `SizeDisplay`. Output-Items: bei `TargetMet=false` orange, bei `HasError=true` rot (über DataTrigger).

Konkretes XAML im Detail während der Umsetzung schreiben — Struktur wie oben.

**Step 3: Build & Start**

Run: `dotnet build src/ImageScaler` und App starten (`dotnet run --project src/ImageScaler`).
Expected: Fenster mit drei sichtbaren Spalten und Bedien-Elementen in der Mitte. Noch keine Funktion.

**Step 4: Commit**

```bash
git add -A
git commit -m "feat(ui): three-column shell with input/settings/output panes"
```

---

## Task 6: WPF — Drag-in (links) + Eingangsliste füllen

**Files:**
- Modify: `src/ImageScaler/MainWindow.xaml.cs`

**Step 1: Drop-Handler für InputList**

- `InputList.Drop` und `InputList.DragOver` (bzw. am umgebenden Border) abonnieren.
- In `DragOver`: `e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? Copy : None`.
- In `Drop`: Dateien aus `(string[])e.Data.GetData(DataFormats.FileDrop)` lesen, nach Bild-Endungen filtern (`.jpg .jpeg .png .bmp .webp .gif`), je Datei ein `ImageItem` mit Originalgrösse (`new FileInfo(path).Length`) zur an `InputList.ItemsSource` gebundenen `ObservableCollection<ImageItem>` hinzufügen.
- Nicht-Bilder ignorieren; optional kurzer Hinweis via `TextBlock`-Status.

**Step 2: „Liste leeren"**

`ClearButton.Click` → Eingangs- (und Ausgangs-)Collection leeren.

**Step 3: Manuelle Verifikation**

App starten, Bilder + eine Nicht-Bild-Datei reinziehen.
Expected: Nur Bilder erscheinen links mit Thumbnail, Name, MB-Grösse. „Liste leeren" leert die Liste.

**Step 4: Commit**

```bash
git add -A
git commit -m "feat(ui): drag-in images into input pane"
```

---

## Task 7: WPF — „Komprimieren" (async) + Output-Liste

**Files:**
- Modify: `src/ImageScaler/MainWindow.xaml.cs`
- Modify: `src/ImageScaler/App.xaml.cs`

**Step 1: Zielgrösse aus UI lesen**

Helper `long GetTargetBytes()`: aus `TargetPreset` (8/10/25/50) oder bei „Eigene…" aus `CustomMb` (parse, Validierung > 0). MB → Bytes (`* 1024 * 1024`).

**Step 2: Session anlegen (App-Lifecycle)**

In `App.xaml.cs`:
- `OnStartup`: `TempSession.CleanupOrphans();` dann `Session = TempSession.Create();`
- Property `public TempSession Session { get; private set; }`

**Step 3: Compress-Handler**

`CompressButton.Click` (async):
- Button deaktivieren, `Progress.Maximum = input.Count`, `Progress.Value = 0`.
- Ausgangs-Collection leeren.
- Für jedes Eingangs-Item `await Task.Run(() => JpegCompressor.CompressToTarget(path, options))`:
  - Erfolg: Bytes in `Session.FolderPath\<originalname>.jpg` schreiben (Namenskollisionen mit Suffix vermeiden), neues `ImageItem` (Output) mit neuer Grösse, `Quality`, `TargetMet` → Output-Collection.
  - Exception (kaputtes Bild): Output-Item mit `HasError=true` (rot), kein File.
  - `Progress.Value++`.
- Button wieder aktivieren.

**Step 4: Manuelle Verifikation**

Bilder reinziehen, Ziel = 10 MB, „Komprimieren".
Expected: Rechts erscheinen JPEGs ≤ Zielgrösse mit neuer MB-Grösse; zu kleine Ziele → orange markiert; UI bleibt während der Verarbeitung bedienbar; Fortschrittsbalken läuft.

**Step 5: Commit**

```bash
git add -A
git commit -m "feat(ui): compress button writes results to temp output pane"
```

---

## Task 8: WPF — Drag-out (rechts) nach Explorer/Discord

**Files:**
- Modify: `src/ImageScaler/MainWindow.xaml.cs`

**Step 1: Drag-out starten**

- `OutputList.PreviewMouseLeftButtonDown` → Startpunkt merken; `OutputList.MouseMove` → bei gedrückter Maustaste und überschrittener Drag-Schwelle:
  - Pfade der **selektierten** Output-Items sammeln (`OutputList.SelectedItems`).
  - `var data = new DataObject(DataFormats.FileDrop, paths.ToArray());`
  - `DragDrop.DoDragDrop(OutputList, data, DragDropEffects.Copy);`

**Step 2: Manuelle Verifikation**

Output-Bild(er) auswählen und in einen Explorer-Ordner sowie ins Discord-Nachrichtenfeld ziehen.
Expected: Datei wird kopiert / Discord nimmt den Upload an. Mehrfachauswahl zieht mehrere Dateien.

**Step 3: Commit**

```bash
git add -A
git commit -m "feat(ui): drag compressed files out to explorer/discord"
```

---

## Task 9: WPF — Cleanup beim Schliessen + Politur

**Files:**
- Modify: `src/ImageScaler/App.xaml.cs`
- Modify: `src/ImageScaler/MainWindow.xaml` (Styling)

**Step 1: Cleanup**

`App.OnExit` → `Session?.Dispose();` (löscht Session-Ordner).

**Step 2: Manuelle Verifikation**

App schliessen, dann `%TEMP%\ImageScaler` prüfen.
Expected: Aktueller Session-Ordner ist weg. (Test: App starten, komprimieren, schliessen → Ordner leer/entfernt.)

**Step 3: Politur (optional, scope-begrenzt)**

- Spaltenüberschriften „Original" / „Einstellungen" / „Ergebnis".
- Leere-Zustand-Hinweis links („Bilder hierher ziehen").
- Konsistente Paddings/Margins, lesbare Schrift.

**Step 4: Commit**

```bash
git add -A
git commit -m "feat(ui): cleanup temp on exit and polish layout"
```

---

## Definition of Done

- `dotnet test` grün (Core-Logik abgedeckt).
- Bilder links reinziehen → „Komprimieren" → rechts JPEGs ≤ Zielgrösse.
- Output-Bilder lassen sich nach Explorer **und** Discord ziehen.
- Nach App-Schliessen ist der Session-Temp-Ordner gelöscht.
- Zielgrösse über Presets **und** freies MB-Feld einstellbar.
