# Sprite Slicer — Design

**Datum:** 2026-07-14
**Status:** Genehmigt

## Zweck

Ein zweites Werkzeug in Simple Image Scaler: ein Sprite-Sheet-Slicer. Ein Bild
wird per Raster in Zellen zerschnitten; jede Zelle fällt als eigenes PNG raus
und kann per Drag & Drop rausgezogen werden.

Kernproblem, das es löst: Sprite-Sheets, bei denen das Raster **nicht** an der
Bildkante beginnt (die Sprites sitzen mittig mit Rand drumherum). Ein Raster von
Kante zu Kante zerschneidet die Sprites. Deshalb sind Ränder und Abstände frei
einstellbar, und einzelne Schnittlinien lassen sich von Hand nachziehen.

## Integration

Die App bekommt oben zwei Tabs:

- **Compress** — die bestehende 3-Spalten-Ansicht, unverändert.
- **Slice** — die neue Slicer-Ansicht.

## Layout des Slice-Tabs

1. **Links (gross):** Bild-Vorschau mit Raster-Overlay, auf die Fenstergrösse
   eingepasst (kein Zoom/Pan). Ein Sprite-Sheet zur Zeit; ein neues Bild
   reinziehen ersetzt das aktuelle.
2. **Mitte:** die Regler + Button „Schneiden".
3. **Rechts:** Liste der geschnittenen Zellen (Thumbnail, Name, Grösse) zum
   Rausziehen — wie im Compress-Tab, inkl. Temp-Aufräumen beim Schliessen.

## Regler

- **Modus:** Umschalter `Nach Anzahl` ⇄ `Nach Zellgrösse`
  - *Anzahl:* Spalten × Zeilen (z.B. 4 × 3)
  - *Zellgrösse:* Breite × Höhe in px (z.B. 32 × 32)
- **Ränder:** Links / Oben / Rechts / Unten (px) — definieren den Raster-Bereich
  innerhalb des Bildes.
- **Abstand:** horizontal / vertikal (px) — Lücke zwischen den Zellen.

Jede Regler-Änderung baut das Raster sofort neu und zeichnet die Vorschau neu.

## Datenmodell: Bänder (nicht Linien)

Reine Schnittlinien können keine Lücken zwischen Zellen abbilden. Deshalb:

- Eine **Spalte** ist ein Band `(Start, End)` in Bildpixeln; analog eine
  **Zeile**.
- **Zellen** = Kreuzprodukt aus Spalten- und Zeilen-Bändern → Rechtecke.
- Bei Abstand 0 fallen benachbarte Kanten zusammen und fühlen sich beim Ziehen
  an wie eine einzelne Linie. Bei Abstand > 0 entsteht die Lücke automatisch.

### Generierung aus den Reglern

- Bereich = `(marginLeft, marginTop)` bis `(width - marginRight, height - marginBottom)`
- *Anzahl-Modus:* `cellW = (areaW - spacingX * (cols - 1)) / cols`;
  Band i: `start = areaX + i * (cellW + spacingX)`, `end = start + cellW`
- *Zellgrössen-Modus:* `cols = floor((areaW + spacingX) / (cellW + spacingX))`;
  Bänder mit der vorgegebenen Zellgrösse ab dem Bereichsanfang.

## Manuelles Bearbeiten

- **Ziehen:** Linie mit der Maus greifen und verschieben; die nächstgelegene
  Bandkante wird gegriffen (Cursor wird zum ↔).
- **Hinzufügen:** Doppelklick auf eine leere Stelle setzt dort eine Linie
  (teilt das Band in zwei).
- **Löschen:** Rechtsklick auf eine Linie entfernt sie (zwei Bänder verschmelzen).
- **Regler gewinnen:** Eine Regler-Änderung baut das Raster neu; manuelle
  Anpassungen gehen dabei verloren. Vorgehen: erst Regler grob, dann von Hand
  fein.

## Schneiden & Ausgabe

- Klick auf **„Schneiden"** schreibt jede Zelle als **PNG** (verlustfrei, mit
  Transparenz) in den Temp-Session-Ordner.
- Benennung: `<originalname>_r0_c0.png`, `<originalname>_r0_c1.png`, …
- **Alle** Zellen werden geschnitten — leere/transparente Zellen werden nicht
  übersprungen.
- Ergebnisse erscheinen rechts, einzeln oder gesammelt rausziehbar; beim
  Schliessen der App gelöscht (bestehende `TempSession`).

## Code-Struktur

Neu in `ImageScaler.Core` (UI-frei, testbar):

- `SliceGrid` — Bänder für Spalten/Zeilen, liefert `Cells` (Rechtecke)
- `GridGenerator` — erzeugt Bänder aus den Reglern (Modus, Ränder, Abstand)
- `ImageSlicer` — schneidet die Zellen mit SkiaSharp aus, liefert PNG-Bytes

## Tests

Die Rechenlogik ist testbar und wird abgedeckt:

- Anzahl-Modus ergibt exakt N × M Zellen.
- Zellgrössen-Modus rechnet die passende Anzahl aus.
- Ränder verschieben den Raster-Bereich korrekt.
- Abstand erzeugt die Lücken zwischen den Zellen.
- Zellen liegen immer vollständig innerhalb des Bildes.

Das Zeichnen und Ziehen im WPF-Canvas wird manuell verifiziert.

## Bewusst nicht enthalten (YAGNI)

- Zoom/Pan in der Vorschau (Einpassen reicht vorerst)
- Überspringen leerer Zellen
- Speichern/Laden von Raster-Vorlagen
