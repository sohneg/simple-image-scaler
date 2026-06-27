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

        var hiData = Encode(hi);
        if (hiData.LongLength <= options.TargetSizeBytes)
            return new CompressionResult { Data = hiData, Quality = hi, TargetMet = true };

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

        var minData = Encode(options.MinQuality);
        return new CompressionResult { Data = minData, Quality = options.MinQuality, TargetMet = false };
    }
}
