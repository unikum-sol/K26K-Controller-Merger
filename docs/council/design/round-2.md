# Design-Council – Runde 2

Grundlage: Renderings nach Umsetzung der zwölf konsolidierten Maßnahmen aus Runde 1. Die Mitglieder prüften ausdrücklich, ob ihre eigenen Punkte gelöst sind.

| Mitglied | Rolle | R1 | **R2** |
|---|---|---|---|
| Claude Opus | Senior Visual Designer | 7 | **8** |
| Claude Sonnet | UX & Informationsarchitektur | 7 | **8,5** |
| Claude Haiku | Accessibility & Augenkomfort | 6 | **8** |
| Claude Fable | Kritischer Product-Design-Lead | 6 | **8** |

## Gelöst laut Council
- Einheitliches Raster: gleiche Inhaltsbreite, gemeinsame Fluchtlinie, eingerückte Trennlinien (Opus)
- Ehrliche Zustände (Aktiv / Bereit / Pausiert / Fehler), „2 von 3 Controllern aktiv“ (alle)
- Eigene Produktsprache: Monogramme, „Gesteuert von“, „Für Spiele sichtbar als Spieler 2“ (Fable)
- Fehlerfall: eine Meldung, eine Handlung (Fable, Sonnet)
- Alle Kontrastprobleme aus R1 in Text, Schaltern, Segmenten und Navigation behoben (Haiku)

## Offene Punkte (konsolidiert)

| # | Problem | Genannt von | Maßnahme für Runde 3 |
|---|---|---|---|
| 1 | „Treiber einrichten“ wirkt deaktiviert, der gesperrte „Wird geprüft …“ leuchtet dagegen voll | alle | Ursache: Im Screenshot-Modus fehlte die Aktion, deshalb war der Button deaktiviert → korrigiert. Wartezustand neu gestaltet: getönte Fläche, Spinner, nicht klickbar, „Schritt 2 von 3“ |
| 2 | Erfolgsdialog zu leer | Opus, Sonnet, Fable | Karte mit den zwei Tray-Gesten (Rechtsklick / Doppelklick) |
| 3 | Reparatur-Stepper suggeriert Abfolge, „Erneut prüfen“ doppelt | Fable | „Das hilft meistens“ als neutrale Liste ohne Linien/Nummern, Dubletten entfernt |
| 4 | Leerzustand doppelt (Hero + Karte) | Opus, Fable | Hero nur noch „Noch kein aktiver Controller“ |
| 5 | Im Fehlerzustand konkurriert die Controller-Liste mit dem Fehler; „ViGEmBus“ im Fließtext | Sonnet, Fable | Controller-Sektion im Fehlerfall ausgeblendet; Text ohne Produktnamen; Hero „Spiele sehen gerade keinen Controller“ |
| 6 | App-Symbol (violett) vs. Akzent (blau); zwei fast gleiche Kacheln im Popup | Opus, Haiku, Fable | Logo-Verlauf im Akzentblau; virtueller Controller bekommt eigene „360“-Kennung |
| 7 | Hinweis-Box ohne Überschrift, zu laut, nicht im Raster | Opus, Fable, Sonnet | Eigene Sektion „Hilfe“ am Seitenende, neutrale Fläche, 40/16-Raster, zwei Sätze |
| 8 | Häkchen vor festen Regeln lesen sich wie Optionen | Fable | Neutrale Aufzählungspunkte |
| 9 | Nur die gewählte Merge-Option wird erklärt | Sonnet | Beide Optionen erklärt, die gewählte hervorgehoben |
| 10 | Schalter allein für Laien nicht eindeutig | Sonnet | „Ein“/„Aus“-Beschriftung wie in den Windows-Einstellungen |
| 11 | „Beenden“ ohne Farbhinweis | Haiku | Symbol in Rot, Text neutral, Rot-Hover bleibt |
| 12 | Tray bei 16 px: „!“ zu klein, Ring zu dünn | Haiku, Opus | Größeres Badge (8 px), pixelgenau, Ring 1,5 px |
| 13 | Heller Slider-Track 2,8:1 | Haiku | #858C97 (≈3,4:1) |
| 14 | Setup-Fußleiste im Hell-Modus heller als Inhalt | Opus | Eigener Footer-Token (#EBEDF0) |
