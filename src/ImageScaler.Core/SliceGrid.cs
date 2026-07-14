namespace ImageScaler.Core;

/// <summary>Zeigt auf eine Kante eines Bandes: welches Band, und ob dessen Start oder Ende.</summary>
public readonly record struct EdgeHandle(int BandIndex, bool IsStart);

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

    private const int MinBandLength = 1;

    // ---- Spalten ----
    public void SplitColumnAt(int x) => SplitAt(Columns, x);
    public EdgeHandle? FindNearestColumnEdge(int x, int tolerance) => FindNearestEdge(Columns, x, tolerance);
    public int GetColumnEdge(EdgeHandle h) => GetEdge(Columns, h);
    public void MoveColumnEdge(EdgeHandle h, int newPos) => MoveEdge(Columns, h, newPos);
    public void RemoveColumnEdge(EdgeHandle h) => RemoveEdge(Columns, h);

    // ---- Zeilen ----
    public void SplitRowAt(int y) => SplitAt(Rows, y);
    public EdgeHandle? FindNearestRowEdge(int y, int tolerance) => FindNearestEdge(Rows, y, tolerance);
    public int GetRowEdge(EdgeHandle h) => GetEdge(Rows, h);
    public void MoveRowEdge(EdgeHandle h, int newPos) => MoveEdge(Rows, h, newPos);
    public void RemoveRowEdge(EdgeHandle h) => RemoveEdge(Rows, h);

    // ---- gemeinsame Logik ----
    private static void SplitAt(List<Band> bands, int pos)
    {
        for (int i = 0; i < bands.Count; i++)
        {
            var b = bands[i];
            if (pos <= b.Start + MinBandLength || pos >= b.End - MinBandLength) continue;
            bands[i] = new Band(b.Start, pos);
            bands.Insert(i + 1, new Band(pos, b.End));
            return;
        }
    }

    private static EdgeHandle? FindNearestEdge(List<Band> bands, int pos, int tolerance)
    {
        EdgeHandle? best = null;
        int bestDist = int.MaxValue;
        for (int i = 0; i < bands.Count; i++)
        {
            foreach (var isStart in new[] { true, false })
            {
                int edge = isStart ? bands[i].Start : bands[i].End;
                int dist = Math.Abs(edge - pos);
                if (dist <= tolerance && dist < bestDist)
                {
                    bestDist = dist;
                    best = new EdgeHandle(i, isStart);
                }
            }
        }
        return best;
    }

    private static int GetEdge(List<Band> bands, EdgeHandle h)
        => h.IsStart ? bands[h.BandIndex].Start : bands[h.BandIndex].End;

    private static void MoveEdge(List<Band> bands, EdgeHandle h, int newPos)
    {
        int oldPos = GetEdge(bands, h);
        // Alle Kanten, die exakt auf der alten Position liegen, wandern mit (Abstand 0 = eine Linie).
        var affected = new List<EdgeHandle>();
        for (int i = 0; i < bands.Count; i++)
        {
            if (bands[i].Start == oldPos) affected.Add(new EdgeHandle(i, true));
            if (bands[i].End == oldPos) affected.Add(new EdgeHandle(i, false));
        }

        int clamped = ClampEdge(bands, affected, newPos);
        foreach (var a in affected)
        {
            var b = bands[a.BandIndex];
            bands[a.BandIndex] = a.IsStart ? new Band(clamped, b.End) : new Band(b.Start, clamped);
        }
    }

    /// <summary>Begrenzt die neue Position so, dass alle betroffenen Bänder gültig bleiben.</summary>
    private static int ClampEdge(List<Band> bands, List<EdgeHandle> affected, int newPos)
    {
        int min = int.MinValue, max = int.MaxValue;
        foreach (var a in affected)
        {
            var b = bands[a.BandIndex];
            if (a.IsStart) max = Math.Min(max, b.End - MinBandLength);
            else min = Math.Max(min, b.Start + MinBandLength);
        }
        if (min != int.MinValue) newPos = Math.Max(newPos, min);
        if (max != int.MaxValue) newPos = Math.Min(newPos, max);
        return newPos;
    }

    private static void RemoveEdge(List<Band> bands, EdgeHandle h)
    {
        int i = h.BandIndex;
        // Verschmelze das Band mit dem Nachbarn auf der Seite der Kante.
        int a = h.IsStart ? i - 1 : i;
        int b = a + 1;
        if (a < 0 || b >= bands.Count) return;
        bands[a] = new Band(bands[a].Start, bands[b].End);
        bands.RemoveAt(b);
    }
}
