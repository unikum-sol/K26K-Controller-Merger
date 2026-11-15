# ControllerMerger 1.0: virtueller Xbox-360-Controller mit automatischem Merging aller Gamepads

## Zusammenfassung

Dieser PR liefert ein Tray-Tool für Windows 10/11 x64. Es erzeugt über **ViGEmBus** dauerhaft **einen** virtuellen Xbox-360-Controller und führt die Eingaben **aller** angeschlossenen Gamepads darauf zusammen: Xbox, DualShock 4, DualSense, Switch Pro, Joy-Cons, HID/DirectInput und Switch 2 (experimentell). Damit können zwei Personen gleichzeitig eine Spielfigur steuern, etwa ein Elternteil, das einem Kind hilft.

## Inhalt

| Bereich | Umsetzung |
|---|---|
| Engine | Dedizierter Input-Thread (MMCSS „Games“, hochauflösender Waitable Timer, 1/2/4/8 ms), allokationsfreier Hot-Path, Report nur bei Änderung |
| Merging | Tasten ODER, Trigger max., Sticks größter Ausschlag (*Ganzer Stick* / *Einzelne Achsen*), D-Pad mit SOCD-Bereinigung, Totzone pro Eingabe, Achsen-Inversion |
| Eingabe | SDL 3.4.20 (offiziell, gepinnt) über eine eigene `LibraryImport`-Schicht; Hotplug per Events |
| Ausgabe | Nefarius.ViGEm.Client; automatischer Reconnect; Rumble an alle aktiven Controller |
| Feedback-Loop-Schutz | XInput-Backend erzwungen, eigener Slot (`UserIndex`) ausgefiltert, Quarantäne bis der Slot bekannt ist; per Log verifiziert |
| Ersteinrichtung | Assistent: Download ViGEmBus 1.22.0 → SHA-256 + Authenticode (gepinnter Herausgeber) → elevated Install → Kontrolle → ggf. Neustart mit automatischem Wiederstart |
| UI | Tray (Rechtsklick: Status-Popup, Doppelklick: Einstellungen), eigenes Design-System mit Hell/Dunkel nach Windows |
| Einstellungen | Totzone, Stick-Modus, Achsen umkehren, Abfrageintervall, Vibration, Autostart, Farbschema, Controller einzeln pausieren |
| Qualität | 20 Unit-Tests (Merge-Logik, Einstellungen), strukturiertes Logging, atomare Settings-Speicherung, Einzelinstanz |
| Distribution | Self-contained Single-File-EXE (≈ 73 MB komprimiert), keine .NET-Installation nötig |

## Entscheidungsprozess

### Architektur-Council (Claude Opus · Sonnet · Haiku)
Vollständiges Protokoll mit allen Bewertungen: [`docs/council/architecture-council.md`](council/architecture-council.md)

- **C# / .NET 10** (einstimmig, 8/9/9) vor Rust (4–5) und C++ (3–6)
- **SDL3** (einstimmig 9) vor Windows.Gaming.Input und eigenen HID-Parsern
- **Eigene P/Invoke-Schicht** (8 vs. 7). Bestätigt dadurch, dass ppy.SDL3-CS eine SDL-3.5-Entwicklerversion mitliefert
- **WPF + Hardcodet** (9/9/8; Tray 2:1)
- **Self-contained Single-File** (einstimmig)
- **Installer-Sicherheit:** Version + SHA-256 + Signer gepinnt (einstimmig)

### Funktionsentscheidungen (vom Nutzer getroffen)
- Kein HidHide, stattdessen ein Hinweis in den Einstellungen und in der README
- Stick-Merge umschaltbar, Standard *Ganzer Stick*
- Controller einzeln pausierbar (keine Pro-Pad-Einstellungen, keine Live-Anzeige, keine Verbindungs-Benachrichtigungen)
- UI auf Deutsch, nur lokales Git

### Design-Council (Claude Opus · Sonnet · Haiku · Fable)
Prozess und alle Runden: [`docs/council/design-council.md`](council/design-council.md)

| Runde | Opus | Sonnet | Haiku | Fable |
|---|---|---|---|---|
| 1 | 7 | 7 | 6 | 6 |
| 2 | 8 | 8,5 | 8 | 8 |
| **3** | **9** | **9** | **9** | **9** |

| Vorher (R1) | Nachher (R3) |
|---|---|
| ![](design/screenshots/before-r1-settings-overview-dark.png) | ![](design/screenshots/settings-overview-dark.png) |

## Verifikation

- `dotnet test`: **20/20 bestanden**
- Smoke-Test mit installiertem ViGEmBus (Debug- und Release-Single-File):
  - `Virtueller Controller belegt XInput-Slot 1`
  - `Controller verbunden: Xbox 360 Controller (… XInput=True/0, self=True)`: der eigene Pad wird korrekt ausgefiltert
  - `Abfragerate: 250 Hz (Intervall 4 ms, High-Res-Timer)`
  - ≈ 0,03 % CPU im Leerlauf, ≈ 78 MB Working Set
- `--snapshot` rendert alle 27 Ansichten bzw. Zustände fehlerfrei in Hell und Dunkel
- Build ohne Warnungen

### Nicht verifiziert (keine Hardware am Testsystem)
- Merging und Rumble mit **echten** physischen Controllern (das Testsystem hatte keinen angeschlossen)
- Installationspfad des Assistenten von Anfang bis Ende (ViGEmBus war bereits installiert; Download, Hash und Signatur wurden separat gegen das Original geprüft)
- Switch-2-Controller

## Offene Punkte / Backlog
- Design-Vorschläge für 10/10 (siehe [Runde 3](council/design/round-3.md#vorschläge-für-1010-bewusst-offen-gelassen))
- Code-Signing der EXE gegen SmartScreen
- Projektlizenz festlegen

🤖 Generated with [Claude Code](https://claude.com/claude-code)
