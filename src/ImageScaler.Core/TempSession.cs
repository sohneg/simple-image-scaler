namespace ImageScaler.Core;

public sealed class TempSession : IDisposable
{
    public static string RootPath { get; } =
        Path.Combine(Path.GetTempPath(), "ImageScaler");

    public string FolderPath { get; }

    private TempSession(string folderPath) => FolderPath = folderPath;

    public static TempSession Create()
    {
        var folder = Path.Combine(RootPath, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return new TempSession(folder);
    }

    /// <summary>Löscht alle Session-Ordner ausser dem aktuellen (Absturz-Reste).</summary>
    public static void CleanupOrphans(string? exceptFolderPath = null)
    {
        if (!Directory.Exists(RootPath)) return;
        foreach (var dir in Directory.GetDirectories(RootPath))
        {
            if (exceptFolderPath is not null &&
                string.Equals(Path.GetFullPath(dir), Path.GetFullPath(exceptFolderPath),
                    StringComparison.OrdinalIgnoreCase))
                continue;
            TryDelete(dir);
        }
    }

    public void Dispose() => TryDelete(FolderPath);

    private static void TryDelete(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
        catch { /* gesperrte Dateien ignorieren – beim nächsten Start aufgeräumt */ }
    }
}
