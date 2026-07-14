namespace ImageScaler.Core;

/// <summary>Eine Zelle des Rasters: Position im Raster (Row/Col) + Rechteck in Bildpixeln.</summary>
public readonly record struct CellRect(int Row, int Col, int X, int Y, int Width, int Height);
