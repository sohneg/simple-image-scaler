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
