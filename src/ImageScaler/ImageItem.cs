using System.ComponentModel;

namespace ImageScaler;

public sealed class ImageItem : INotifyPropertyChanged
{
    public required string FilePath { get; init; }
    public required string FileName { get; init; }

    private long _sizeBytes;
    public required long SizeBytes
    {
        get => _sizeBytes;
        set { _sizeBytes = value; OnChanged(nameof(SizeBytes)); OnChanged(nameof(SizeDisplay)); }
    }

    // Sprite-Zellen sind oft nur ein paar hundert Byte – in MB wären sie alle "0.00".
    public string SizeDisplay => SizeBytes >= 1024 * 1024
        ? $"{SizeBytes / 1024.0 / 1024.0:0.00} MB"
        : $"{SizeBytes / 1024.0:0.0} KB";

    // Nur für Output-Items relevant:
    public int Quality { get; set; }
    public bool TargetMet { get; set; } = true;
    public bool HasError { get; set; }
    public string StatusText { get; set; } = "";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
