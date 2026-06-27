using ImageScaler.Core;
using Xunit;

namespace ImageScaler.Tests;

public class TempSessionTests
{
    [Fact]
    public void Create_MakesSessionFolder_UnderRoot()
    {
        using var session = TempSession.Create();
        Assert.True(Directory.Exists(session.FolderPath));
        Assert.StartsWith(TempSession.RootPath, session.FolderPath);
    }

    [Fact]
    public void Dispose_DeletesSessionFolder()
    {
        var session = TempSession.Create();
        var folder = session.FolderPath;
        File.WriteAllText(Path.Combine(folder, "x.txt"), "hi");
        session.Dispose();
        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    public void CleanupOrphans_RemovesOtherSessionFolders()
    {
        var orphan = Path.Combine(TempSession.RootPath, "orphan_test");
        Directory.CreateDirectory(orphan);
        using var session = TempSession.Create();

        TempSession.CleanupOrphans(exceptFolderPath: session.FolderPath);

        Assert.False(Directory.Exists(orphan));
        Assert.True(Directory.Exists(session.FolderPath));
    }
}
