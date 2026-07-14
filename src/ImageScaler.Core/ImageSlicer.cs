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
