namespace ImageScaler.Core;

/// <summary>Ein Spalten- oder Zeilen-Band in Bildpixeln. End ist exklusiv.</summary>
public readonly record struct Band(int Start, int End)
{
    public int Length => End - Start;
}
