using System.Collections.Generic;
using System.ComponentModel;

namespace ImageScaler;

/// <summary>
/// Simple in-app localization. Default language is English ("en").
/// XAML binds to the indexer, e.g. Text="{Binding [Compress], Source={x:Static local:Loc.Instance}}".
/// Switching the language raises a change for "Item[]" so all bound strings refresh at runtime.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    private string _lang = "en";
    public string Language => _lang;

    private static readonly Dictionary<string, Dictionary<string, string>> Map = new()
    {
        ["en"] = new()
        {
            ["Settings"] = "Settings",
            ["Original"] = "Original",
            ["Result"] = "Result  (drag out to share)",
            ["DragHint"] = "Drag images here",
            ["OutputHint"] = "Compressed images appear here",
            ["TargetSize"] = "Target size",
            ["CustomSize"] = "Custom size (MB)",
            ["Custom"] = "Custom…",
            ["Format"] = "Format: JPEG",
            ["Compress"] = "Compress",
            ["ClearList"] = "Clear list",
            ["Language"] = "Language",
            ["NoImages"] = "No images to compress.",
            ["InvalidSize"] = "Please enter a valid size (MB).",
            ["NoSession"] = "No session – please restart.",
            ["Done"] = "Done.",
            ["DoneFailed"] = "Done – {0} failed.",
            ["Ignored"] = "{0} non-image file(s) ignored.",
            ["Quality"] = "Quality {0}",
            ["TargetNotReached"] = "Target size not reached",
            ["ErrorPrefix"] = "Error: ",
            ["TabCompress"] = "Compress",
            ["TabSlice"] = "Slice",
        },
        ["de"] = new()
        {
            ["Settings"] = "Einstellungen",
            ["Original"] = "Original",
            ["Result"] = "Ergebnis  (zum Verschicken rausziehen)",
            ["DragHint"] = "Bilder hierher ziehen",
            ["OutputHint"] = "Komprimierte Bilder erscheinen hier",
            ["TargetSize"] = "Zielgrösse",
            ["CustomSize"] = "Eigene Grösse (MB)",
            ["Custom"] = "Eigene…",
            ["Format"] = "Format: JPEG",
            ["Compress"] = "Komprimieren",
            ["ClearList"] = "Liste leeren",
            ["Language"] = "Sprache",
            ["NoImages"] = "Keine Bilder zum Komprimieren.",
            ["InvalidSize"] = "Bitte eine gültige Grösse (MB) eingeben.",
            ["NoSession"] = "Keine Session – Neustart nötig.",
            ["Done"] = "Fertig.",
            ["DoneFailed"] = "Fertig – {0} fehlgeschlagen.",
            ["Ignored"] = "{0} Nicht-Bild-Datei(en) ignoriert.",
            ["Quality"] = "Qualität {0}",
            ["TargetNotReached"] = "Zielgrösse nicht erreicht",
            ["ErrorPrefix"] = "Fehler: ",
            ["TabCompress"] = "Komprimieren",
            ["TabSlice"] = "Schneiden",
        },
    };

    public string this[string key] =>
        Map[_lang].TryGetValue(key, out var v) ? v : key;

    public void SetLanguage(string lang)
    {
        if (lang == _lang || !Map.ContainsKey(lang)) return;
        _lang = lang;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
