using ImageScaler.Core;
using Xunit;

namespace ImageScaler.Tests;

public class SliceGridTests
{
    [Fact]
    public void Cells_AreCrossProductOfColumnsAndRows()
    {
        var grid = new SliceGrid();
        grid.Columns.AddRange(new[] { new Band(0, 10), new Band(10, 20) });
        grid.Rows.AddRange(new[] { new Band(0, 5) });

        var cells = grid.Cells.ToList();

        Assert.Equal(2, cells.Count);
        Assert.Contains(cells, c => c is { Row: 0, Col: 0, X: 0, Y: 0, Width: 10, Height: 5 });
        Assert.Contains(cells, c => c is { Row: 0, Col: 1, X: 10, Y: 0, Width: 10, Height: 5 });
    }

    [Fact]
    public void SplitColumnAt_DividesTheContainingBand()
    {
        var grid = new SliceGrid();
        grid.Columns.Add(new Band(0, 100));

        grid.SplitColumnAt(40);

        Assert.Equal(2, grid.Columns.Count);
        Assert.Equal(new Band(0, 40), grid.Columns[0]);
        Assert.Equal(new Band(40, 100), grid.Columns[1]);
    }

    [Fact]
    public void SplitColumnAt_IgnoresPositionOutsideAnyBand()
    {
        var grid = new SliceGrid();
        grid.Columns.Add(new Band(0, 40));
        grid.Columns.Add(new Band(60, 100));  // Lücke 40..60

        grid.SplitColumnAt(50);   // in der Lücke -> nichts tun

        Assert.Equal(2, grid.Columns.Count);
    }

    [Fact]
    public void FindNearestColumnEdge_ReturnsClosestWithinTolerance()
    {
        var grid = new SliceGrid();
        grid.Columns.Add(new Band(0, 40));
        grid.Columns.Add(new Band(40, 100));

        var handle = grid.FindNearestColumnEdge(43, tolerance: 5);

        Assert.NotNull(handle);
        Assert.Equal(40, grid.GetColumnEdge(handle!.Value));
        Assert.Null(grid.FindNearestColumnEdge(70, tolerance: 5));  // zu weit weg
    }

    [Fact]
    public void MoveColumnEdge_MovesCoincidentEdgesTogether()
    {
        var grid = new SliceGrid();
        grid.Columns.Add(new Band(0, 40));
        grid.Columns.Add(new Band(40, 100));   // Kanten liegen aufeinander (Abstand 0)

        var handle = grid.FindNearestColumnEdge(40, tolerance: 2)!.Value;
        grid.MoveColumnEdge(handle, 60);

        Assert.Equal(new Band(0, 60), grid.Columns[0]);
        Assert.Equal(new Band(60, 100), grid.Columns[1]);
    }

    [Fact]
    public void MoveColumnEdge_ClampsSoBandKeepsMinimumWidth()
    {
        var grid = new SliceGrid();
        grid.Columns.Add(new Band(0, 40));
        grid.Columns.Add(new Band(40, 100));

        var handle = grid.FindNearestColumnEdge(40, tolerance: 2)!.Value;
        grid.MoveColumnEdge(handle, -50);   // weit über den Anfang hinaus

        Assert.True(grid.Columns[0].Length >= 1);
        Assert.True(grid.Columns[1].Length >= 1);
    }

    [Fact]
    public void RemoveColumnEdge_MergesTheTwoBands()
    {
        var grid = new SliceGrid();
        grid.Columns.Add(new Band(0, 40));
        grid.Columns.Add(new Band(40, 100));

        var handle = grid.FindNearestColumnEdge(40, tolerance: 2)!.Value;
        grid.RemoveColumnEdge(handle);

        Assert.Single(grid.Columns);
        Assert.Equal(new Band(0, 100), grid.Columns[0]);
    }

    [Fact]
    public void MoveRowEdge_WorksTheSameAsColumns()
    {
        var grid = new SliceGrid();
        grid.Rows.Add(new Band(0, 30));
        grid.Rows.Add(new Band(30, 90));

        var handle = grid.FindNearestRowEdge(30, tolerance: 2)!.Value;
        grid.MoveRowEdge(handle, 50);

        Assert.Equal(new Band(0, 50), grid.Rows[0]);
        Assert.Equal(new Band(50, 90), grid.Rows[1]);
    }

    [Fact]
    public void MoveColumnEdge_WithGap_MovesOnlyThatEdge()
    {
        // Abstand vorhanden -> die Kanten liegen NICHT aufeinander, nur die gegriffene bewegt sich
        var grid = new SliceGrid();
        grid.Columns.Add(new Band(0, 40));
        grid.Columns.Add(new Band(50, 100));   // Lücke 40..50

        var handle = grid.FindNearestColumnEdge(40, tolerance: 2)!.Value;
        grid.MoveColumnEdge(handle, 45);

        Assert.Equal(new Band(0, 45), grid.Columns[0]);
        Assert.Equal(new Band(50, 100), grid.Columns[1]);   // unverändert
    }
}
