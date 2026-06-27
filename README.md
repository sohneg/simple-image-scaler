# ImageScaler

Ein schlankes Windows-Tool zum **Komprimieren von Bildern auf eine Zielgrösse** (z.B. 10 MB) – ideal, um Fotos schnell unter ein Upload-Limit (Discord, Mail, Web) zu bringen.

Drei Spalten, ganz wie ein Datei-Manager: links die Originale reinziehen, in der Mitte die Zielgrösse wählen, rechts die fertigen Bilder rausziehen. Die Ergebnisse sind temporär und werden beim Schliessen automatisch aufgeräumt.

> ⚠️ **Windows only.** ImageScaler ist eine WPF-App und läuft nur unter Windows (x64).

---

## Funktionen

- 🎯 **Zielgrösse pro Bild** – feste Presets (8 / 10 / 25 / 50 MB) oder eigener Wert in MB
- 🔍 **Beste Qualität bei gegebener Grösse** – die Auflösung bleibt erhalten; nur die JPEG-Qualität wird per Binärsuche so hoch wie möglich gehalten, bis das Bild unter die Zielgrösse passt
- 🖱️ **Drag & Drop rein und raus** – Bilder links reinziehen, fertige Bilder rechts direkt nach Discord, in einen Ordner oder in eine E-Mail ziehen
- 🧹 **Automatisches Aufräumen** – die komprimierten Bilder liegen nur temporär und werden beim Beenden gelöscht (verwaiste Reste eines Absturzes werden beim nächsten Start entfernt)
- 🟠 **Klare Rückmeldung** – pro Bild werden Qualität und neue Grösse angezeigt; nicht erreichbare Zielgrössen werden orange, Fehler rot markiert
- 📦 **Keine Installation nötig** – eine einzige `.exe`, die komplette Runtime ist eingebettet

---

## Verwendung

1. **Bilder reinziehen** – Ziehe ein oder mehrere Bilder aus dem Explorer in die **linke** Spalte („Original"). Unterstützt werden `.jpg`, `.jpeg`, `.png`, `.bmp`, `.webp`, `.gif`. Andere Dateien werden ignoriert.
2. **Zielgrösse wählen** – In der **Mitte** ein Preset (8 / 10 / 25 / 50 MB) auswählen oder `Eigene…` und einen MB-Wert eintippen.
3. **Komprimieren** – Auf **Komprimieren** klicken. Der Fortschrittsbalken läuft, die Ergebnisse erscheinen rechts als JPEG mit neuer Grösse und Qualitäts-Info.
4. **Rausziehen** – In der **rechten** Spalte ein oder mehrere Bilder markieren und per Drag & Drop dorthin ziehen, wo du sie brauchst (Discord-Chat, Ordner, Mail …).
5. **Schliessen** – Beim Beenden werden die temporären Ergebnisse automatisch gelöscht. Was du vorher rausgezogen hast, bleibt natürlich erhalten.

> 💡 Das Ausgabeformat ist immer **JPEG**, weil sich damit die Zielgrösse am feinsten treffen lässt und es überall geöffnet werden kann. (PNG-Transparenz geht dabei verloren.)

---

## Download & Start

1. Lade die aktuelle `ImageScaler.exe` aus den [Releases](../../releases) herunter.
2. Doppelklick – fertig. Eine Installation ist nicht nötig.

Beim ersten Start kann der **Windows-SmartScreen** warnen („Unbekannter Herausgeber"), weil die `.exe` nicht signiert ist. Über **Weitere Informationen → Trotzdem ausführen** startest du sie.

---

## Aus dem Quellcode bauen

**Voraussetzungen:** [.NET SDK 11](https://dotnet.microsoft.com/download) (oder neuer), Windows x64.

```bash
# Repository klonen
git clone https://github.com/<dein-user>/ImageScaler.git
cd ImageScaler

# Tests ausführen
dotnet test

# Im Debug-Modus starten
dotnet run --project src/ImageScaler
```

### Eine eigenständige .exe erzeugen

```bash
dotnet publish src/ImageScaler/ImageScaler.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish_out
```

Die fertige `ImageScaler.exe` liegt danach im Ordner `publish_out` (ca. 68 MB, da .NET-Runtime und SkiaSharp eingebettet sind).

---

## Projektstruktur

```
src/
  ImageScaler.Core/     # UI-freie Komprimierungs-Logik (JpegCompressor, TempSession)
  ImageScaler/          # WPF-App (3-Spalten-UI, Drag & Drop)
tests/
  ImageScaler.Tests/    # Unit-Tests für die Kern-Logik
docs/plans/             # Design- und Implementierungs-Dokument
```

---

## Technik

- **[.NET 11](https://dotnet.microsoft.com/) / WPF** – Desktop-UI
- **[SkiaSharp](https://github.com/mono/SkiaSharp)** (MIT) – Bild-Encoding
- **xUnit** – Tests

Die Komprimierung lädt das Bild, encodiert es per Binärsuche bei verschiedenen JPEG-Qualitätsstufen (5–95) wiederholt im Speicher und wählt die höchste Qualität, deren Datei noch unter der Zielgrösse liegt. Liegt das Bild schon darunter, wird es mit maximaler Qualität gespeichert; ist die Zielgrösse selbst bei minimaler Qualität nicht erreichbar, wird das Bild trotzdem ausgegeben und markiert.

---

## Lizenz

Noch keine Lizenz festgelegt. Wenn du das Projekt öffentlich und für andere nutzbar machen möchtest, füge eine `LICENSE`-Datei hinzu (z.B. [MIT](https://choosealicense.com/licenses/mit/)). Die verwendete Bibliothek SkiaSharp steht unter der MIT-Lizenz und ist auch für kommerzielle Nutzung frei.
