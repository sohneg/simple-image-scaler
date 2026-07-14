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
