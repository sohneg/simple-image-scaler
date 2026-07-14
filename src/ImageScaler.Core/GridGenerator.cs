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

        grid.Columns.AddRange(BuildBands(areaX, areaW, p.SpacingX, p.Mode, p.Columns, p.CellWidth));
        grid.Rows.AddRange(BuildBands(areaY, areaH, p.SpacingY, p.Mode, p.Rows, p.CellHeight));

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
