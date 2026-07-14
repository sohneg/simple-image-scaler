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
}
