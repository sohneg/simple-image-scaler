# ImageScaler — Design

**Datum:** 2026-06-27
**Status:** Genehmigt

## Zweck

Eine Windows-Desktop-App, mit der man Bilder per Drag & Drop auf eine
einstellbare Zielgrösse (z.B. 10 MB) komprimiert. Die komprimierten Ergebnisse
sind temporär und können direkt nach Discord oder in einen anderen Ordner
gezogen werden. Beim Schliessen der App werden die Temp-Ergebnisse gelöscht.

## Tech-Stack

- **UI:** WPF (.NET 11), modern gestylt
- **Bild-Library:** SkiaSharp (MIT-Lizenz — gratis, auch kommerziell/zum Verkaufen)
- **Tests:** xUnit (oder vergleichbar)

## Layout — 3 Spalten

1. **Links — Eingang:** Original-Bilder reinziehen. Anzeige: Thumbnail,
   Dateiname, Originalgrösse.
2. **Mitte — Einstellungen:** Zielgrössen-Dropdown (8 / 10 / 25 / 50 MB) +
   freies MB-Eingabefeld, Format-Anzeige (JPEG), Button „Komprimieren",
   Fortschrittsanzeige, Button „Liste leeren".
3. **Rechts — Output:** Komprimierte JPEGs mit Thumbnail, neuer Grösse,
   Qualitäts-Info. Mehrfach-Auswahl möglich → mehrere Bilder gleichzeitig
   nach Discord/Explorer ziehen.

## Komprimierungs-Logik

- Methode: **Binärsuche über die JPEG-Qualität** (Bereich ca. 5–95). Das Bild
  wird wiederholt in den Speicher encodiert; gewählt wird die höchste Qualität,
  die noch unter die Zielgrösse passt.
- **Auflösung bleibt gleich** (nur Qualität wird reduziert).
- **Zielgrösse gilt pro Bild** (passt zum Discord-Limit pro Datei).
- Sonderfall „schon klein genug": Bild ist bereits unter Zielgrösse → mit hoher
  Qualität (z.B. 90) speichern, kein unnötiges Verschlechtern.
- Sonderfall „nicht erreichbar": selbst bei minimaler Qualität zu gross → Bild
  trotzdem ausgeben, aber orange markiert („Zielgrösse nicht erreicht").
- Ausgabeformat: **JPEG**.

## Temp-Dateien & Aufräumen

- Ergebnisse werden in `%TEMP%\ImageScaler\<session-guid>\` geschrieben.
- Es sind echte Dateien auf der Platte → Drag-out nach Discord/Explorer
  funktioniert über `DataObject` mit `FileDrop`.
- Beim Schliessen der App (`OnExit`) wird der Session-Ordner gelöscht.
- Beim Start werden verwaiste Alt-Session-Ordner aufgeräumt (Absturz-Schutz).

## Fehlerbehandlung

- Nicht-Bild-Dateien beim Reinziehen werden ignoriert (kurzer Hinweis).
- Kaputte/unlesbare Bilder werden übersprungen und rot markiert.
- Komprimierung läuft asynchron, damit das UI flüssig bleibt; Fortschritt pro
  Bild sichtbar.

## Projektstruktur

- `ImageScaler` — WPF-App (UI, Drag & Drop, Temp-Verwaltung)
- `ImageScaler.Core` — Komprimierungs-Logik, UI-frei und testbar
  (`CompressToTarget` o.ä.)
- `ImageScaler.Tests` — Unit-Tests für die Kern-Logik

## Test-Schwerpunkte

- Ergebnis ist ≤ Zielgrösse, wenn erreichbar.
- Qualitätsgrenzen werden eingehalten.
- „Schon klein genug"-Fall greift korrekt (kein unnötiges Verschlechtern).
- „Nicht erreichbar"-Fall liefert trotzdem ein Bild + Markierung.
