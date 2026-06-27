using ImageScaler.Core;
using SkiaSharp;
using Xunit;

namespace ImageScaler.Tests;

public class JpegCompressorTests
{
    // Erzeugt ein "verrauschtes" Bild, das als JPEG nicht trivial klein wird.
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
        var target = 200_000L;
        var result = JpegCompressor.CompressToTarget(path, new CompressionOptions { TargetSizeBytes = target });

        Assert.True(result.TargetMet);
        Assert.True(result.Size <= target, $"Size {result.Size} > target {target}");
        Assert.InRange(result.Quality, 5, 95);
        File.Delete(path);
    }

    [Fact]
    public void CompressToTarget_KeepsHighQuality_WhenAlreadySmall()
    {
        var path = CreateNoiseImage(200, 200);
        var result = JpegCompressor.CompressToTarget(path,
            new CompressionOptions { TargetSizeBytes = 50_000_000L });

        Assert.True(result.TargetMet);
        Assert.Equal(95, result.Quality);
        File.Delete(path);
    }

    [Fact]
    public void CompressToTarget_ReturnsImageAndFlag_WhenTargetUnreachable()
    {
        var path = CreateNoiseImage(2000, 2000);
        var result = JpegCompressor.CompressToTarget(path,
            new CompressionOptions { TargetSizeBytes = 500L });

        Assert.False(result.TargetMet);
        Assert.True(result.Data.Length > 0);
        Assert.Equal(5, result.Quality);
        File.Delete(path);
    }
}
