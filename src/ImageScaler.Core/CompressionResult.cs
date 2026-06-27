namespace ImageScaler.Core;

public sealed class CompressionResult
{
    public required byte[] Data { get; init; }
    public required int Quality { get; init; }
    public long Size => Data.LongLength;
    /// <summary>True, wenn die Zielgrösse erreicht wurde.</summary>
    public required bool TargetMet { get; init; }
}
