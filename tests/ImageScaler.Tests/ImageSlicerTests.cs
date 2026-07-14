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

    [Fact]
    public void Slice_WithSpacing_CutsOnlyTheCellsNotTheGaps()
    {
        // 64x64, 2 Spalten mit 4px Abstand -> Zelle = (64-4)/2 = 30 breit
        var path = CreateQuadrantImage();
        var grid = GridGenerator.Generate(64, 64, new GridParams
        {
            Mode = GridMode.ByCount, Columns = 2, Rows = 1, SpacingX = 4
        });

        var results = ImageSlicer.Slice(path, grid);

        Assert.Equal(2, results.Count);
        foreach (var r in results)
        {
            using var cell = SKBitmap.Decode(r.PngData);
            Assert.Equal(30, cell.Width);
            Assert.Equal(64, cell.Height);
        }
        File.Delete(path);
    }
}
