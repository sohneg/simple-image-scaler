using System.Windows;
using ImageScaler.Core;

namespace ImageScaler;

public partial class App : Application
{
    public TempSession? Session { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        TempSession.CleanupOrphans();        // Reste alter Sessions aufräumen
        Session = TempSession.Create();      // Session für diesen Lauf
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Session?.Dispose();                  // komprimierte Temp-Bilder löschen
        base.OnExit(e);
    }
}
