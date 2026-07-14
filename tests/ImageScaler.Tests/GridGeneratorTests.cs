using ImageScaler.Core;
using Xunit;

namespace ImageScaler.Tests;

public class GridGeneratorTests
{
    [Fact]
    public void ByCount_ProducesExactCellCountAndSize()
    {
        var grid = GridGenerator.Generate(400, 300,
            new GridParams { Mode = GridMode.ByCount, Columns = 4, Rows = 3 });

        Assert.Equal(4, grid.Columns.Count);
        Assert.Equal(3, grid.Rows.Count);
        Assert.Equal(12, grid.Cells.Count());
        Assert.All(grid.Columns, b => Assert.Equal(100, b.Length));
        Assert.All(grid.Rows, b => Assert.Equal(100, b.Length));
        Assert.Equal(new Band(0, 100), grid.Columns[0]);
        Assert.Equal(new Band(300, 400), grid.Columns[3]);
    }

    [Fact]
    public void Margins_ShiftAndShrinkTheGridArea()
    {
        var grid = GridGenerator.Generate(100, 100, new GridParams
        {
            Mode = GridMode.ByCount, Columns = 2, Rows = 1,
            MarginLeft = 10, MarginRight = 10, MarginTop = 20, MarginBottom = 20
        });

        Assert.Equal(new Band(10, 50), grid.Columns[0]);
        Assert.Equal(new Band(50, 90), grid.Columns[1]);
        Assert.Equal(new Band(20, 80), grid.Rows[0]);
    }

    [Fact]
    public void BySize_DerivesCountFromCellSize()
    {
        var grid = GridGenerator.Generate(128, 96,
            new GridParams { Mode = GridMode.BySize, CellWidth = 32, CellHeight = 32 });

        Assert.Equal(4, grid.Columns.Count);
        Assert.Equal(3, grid.Rows.Count);
        Assert.Equal(new Band(0, 32), grid.Columns[0]);
        Assert.Equal(new Band(96, 128), grid.Columns[3]);
    }

    [Fact]
    public void Spacing_CreatesGapsBetweenCells()
    {
        var grid = GridGenerator.Generate(100, 10, new GridParams
        {
            Mode = GridMode.ByCount, Columns = 2, Rows = 1, SpacingX = 10
        });

        Assert.Equal(new Band(0, 45), grid.Columns[0]);
        Assert.Equal(new Band(55, 100), grid.Columns[1]);
    }

    [Fact]
    public void BySize_WithSpacing_StopsBeforeOverflowing()
    {
        var grid = GridGenerator.Generate(100, 30, new GridParams
        {
            Mode = GridMode.BySize, CellWidth = 30, CellHeight = 30, SpacingX = 10
        });

        Assert.Equal(2, grid.Columns.Count);
        Assert.Equal(new Band(0, 30), grid.Columns[0]);
        Assert.Equal(new Band(40, 70), grid.Columns[1]);
    }

    [Fact]
    public void AllCells_StayInsideImageBounds()
    {
        var grid = GridGenerator.Generate(333, 177, new GridParams
        {
            Mode = GridMode.ByCount, Columns = 7, Rows = 5,
            MarginLeft = 3, MarginTop = 4, MarginRight = 5, MarginBottom = 6, SpacingX = 2, SpacingY = 3
        });

        Assert.NotEmpty(grid.Cells);
        Assert.All(grid.Cells, c =>
        {
            Assert.True(c.X >= 0 && c.Y >= 0);
            Assert.True(c.X + c.Width <= 333, $"cell right {c.X + c.Width} > 333");
            Assert.True(c.Y + c.Height <= 177, $"cell bottom {c.Y + c.Height} > 177");
            Assert.True(c.Width > 0 && c.Height > 0);
        });
    }
}
