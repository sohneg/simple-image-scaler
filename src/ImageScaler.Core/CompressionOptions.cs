namespace ImageScaler.Core;

public sealed class CompressionOptions
{
    /// <summary>Zielgrösse pro Bild in Bytes.</summary>
    public required long TargetSizeBytes { get; init; }
    public int MinQuality { get; init; } = 5;
    public int MaxQuality { get; init; } = 95;
}
