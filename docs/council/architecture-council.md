# Architektur- & Implementierungs-Council

Der Council bestand aus drei Claude-Modellen mit bewusst unterschiedlichen Perspektiven. Alle drei bekamen dieselbe Problembeschreibung und dieselben Optionen und bewerteten ohne gegenseitige Kenntnis. Danach wurde konsolidiert. Bei Gleichstand gab die Mehrheit bzw. die Robustheits-Perspektive den Ausschlag.

| Mitglied | Perspektive |
|---|---|
| Claude Opus | Robustheit, Latenz, Low-Level-Korrektheit unter Windows |
| Claude Sonnet | Wartbarkeit, Ökosystem, Erweiterbarkeit, Developer-Experience |
| Claude Haiku | Endnutzer-Erfahrung, Installation, Ressourcenverbrauch, Sicherheit |

**Ausgangslage der Umgebung:** .NET 10 SDK vorhanden, keine Rust-Toolchain, ViGEmBus v1.22.0 (Projekt EOL/archiviert, aber funktionsfähig), SDL 3.4.20 aktuell.

---

## 1. Sprache / Runtime

| Option | Opus | Sonnet | Haiku |
|---|---|---|---|
| **C# / .NET 10** | **8** | **9** | **9** |
| Rust (vigem-client + sdl3) | 5 | 4 | 5 |
| C++ | 5 | 3 | 6 |

**Entscheidung: C# / .NET 10 (einstimmig).**
Nefarius.ViGEm.Client ist die Referenz-Implementierung (rein managed), WPF ist nativ verfügbar, und der Hot-Path lässt sich allokationsfrei schreiben, sodass der GC keine Rolle spielt. Rust scheiterte an fehlender Toolchain, dünnem UI-Ökosystem für eine hochwertige Oberfläche und kaum gepflegter ViGEm-Crate. C++ hätte kaum Latenzgewinn gebracht, aber hohe UI-Kosten verursacht.

## 2. Input-Bibliothek

| Option | Opus | Sonnet | Haiku |
|---|---|---|---|
| **SDL3 (Gamepad-API + HIDAPI)** | **9** | **9** | **9** |
| Windows.Gaming.Input | 4 | 4 | 5 |
| Eigene HID-Parser | 3 | 3 | 3 |
| Kombination | 5 | 6 | – |

**Entscheidung: SDL3 3.4.x (einstimmig).** SDL deckt XInput, DualShock 4, DualSense, Switch Pro, Joy-Cons, generische DirectInput/HID-Geräte und (experimentell) Switch-2-Controller ab, inklusive Rumble, Kalibrierung, Mapping-Datenbank und Hotplug. Der Pflegeaufwand liegt bei SDL. WGI unterstützt keine PS-/Switch-Pads und hat Probleme im Hintergrund, und eigene Parser wären „ein endloses Fass“.

### Bindings

| Option | Opus | Sonnet |
|---|---|---|
| **Eigene schlanke `LibraryImport`-Schicht** | **8** | **8** |
| ppy.SDL3-CS | 7 | 7 |

**Entscheidung: eigene P/Invoke-Schicht (`Interop/Sdl.cs`, ~35 Funktionen).** Das gibt volle Kontrolle über Hints und Version, ohne Abhängigkeit vom Release-Takt eines Drittprojekts.
*Nachträgliche Bestätigung:* Eine Prüfung zeigte, dass das NuGet-Paket ppy.SDL3-CS eine **SDL-3.5.0-Entwicklerversion** mitliefert. Daher wird die offizielle SDL-**3.4.20**-Runtime gepinnt (`tools/fetch-sdl.ps1`, SHA-256-geprüft) und unter `lib/SDL3` versioniert.

## 3. Schutz vor dem Feedback-Loop (kritisch)

Der eigene virtuelle Xbox-360-Controller erscheint unter Windows wie ein echtes XInput-Gerät. Würde er wieder eingelesen, entstünde eine Rückkopplung: Tasten würden „hängen“, Rumble würde sich selbst verstärken.

| Option | Opus | Sonnet |
|---|---|---|
| (a) RawInput/WGI aus, XInput-Backend, Slot == ViGEm-UserIndex filtern | 8 | 6 |
| (b) Eigene VID/PID | 6 | 7 |
| (c) Gerätepfad/Parent-Enumerator | 6 | 8 |
| **(d) Kombination** | **9** | **9** |

**Entscheidung: Kombination, Kern ist (a):**
1. SDL-Hints `SDL_JOYSTICK_RAWINPUT=0`, `SDL_JOYSTICK_WGI=0`, `SDL_JOYSTICK_HIDAPI_XBOX=0`. Damit laufen Xbox-Pads garantiert über das XInput-Backend und haben einen eindeutigen Slot (`XInput#n`, GUID-Marker `'x'`).
2. **Reihenfolge:** zuerst ViGEm verbinden, dann den `UserIndex` lesen (Retry bis 1 s, danach periodisch), erst danach SDL starten.
3. **Quarantäne:** Solange der Slot unbekannt ist, werden *alle* XInput-Pads ignoriert („lieber kurz ignorieren als loopen“).
4. Rumble wird nie an den eigenen Slot gegeben.
5. Nach einem Treiber-Reconnect wird der Slot neu gelesen.

*Verworfen:* (b), weil eine eigene VID/PID die Kompatibilität mit Spielen und Steam gefährdet. Der von Sonnet vorgeschlagene „Loop-Detektor“ (Pad spiegelt exakt die Ausgabe) wurde bewusst **nicht** umgesetzt: Ein einzelner echter Controller spiegelt die Ausgabe zwangsläufig, das ergäbe False Positives.
*Verifiziert:* Im Smoke-Test meldete das Log `Virtueller Controller belegt XInput-Slot 1` und `Controller verbunden: Xbox 360 Controller (… XInput=True/0, self=True)`. Der eigene Pad wird also korrekt erkannt und ausgeschlossen.

## 4. UI-Framework & Tray

| Option | Opus | Sonnet | Haiku |
|---|---|---|---|
| **WPF (eigene Styles, WindowChrome)** | **9** | **9** | **8** |
| WinUI 3 | 6 | 5 | 5 |
| Avalonia | 6 | 6 | 6 |
| WinForms | 3 | 3 | – |

| Tray | Opus | Sonnet | Haiku |
|---|---|---|---|
| **Hardcodet.NotifyIcon.Wpf** | 7 | **8** | **8** |
| Eigener Shell_NotifyIcon-Wrapper | **8** | 5 | – |

**Entscheidung: WPF mit eigenem Design-System, Tray über Hardcodet (2:1).** Das Status-Popup ist ein eigenes, randloses WPF-Fenster, das per `SetWindowPos` pixelgenau am Infobereich positioniert wird. Damit ist Opus' Hauptargument (Popup-Positionierung, Fokus-Dismiss) auch mit Hardcodet erfüllt. Fenster werden nur bei Bedarf erzeugt und beim Schließen freigegeben (Sonnet), damit der Ruhe-RAM gering bleibt.

## 5. Threading & Timing

**Einstimmig bestätigt:**
- Dedizierter Input-Thread, SDL ausschließlich auf diesem Thread, nur `SDL_INIT_GAMEPAD`.
- `CreateWaitableTimerExW(CREATE_WAITABLE_TIMER_HIGH_RESOLUTION)` mit absoluter, driftfreier Deadline, kein globales `timeBeginPeriod`.
- Thread über MMCSS als `"Games"` eingestuft (`AvSetMmThreadCharacteristics`).
- Pro Tick: Hotplug-Events, Zustände lesen, mergen, **Report nur bei Änderung** senden (plus Keep-Alive alle 500 ms).
- Rumble-Callback (Treiber-Thread) schreibt nur in eine atomare Mailbox, angewendet wird auf dem Input-Thread, aufgefrischt jede Sekunde.
- UI liest unveränderliche Snapshots (10 Hz), die UI-Aktualisierung läuft nur bei offenem Fenster.

*Gemessen:* 250 Hz bei 4 ms Intervall, **≈ 0,03 % CPU** im Leerlauf, ~76 MB Working Set (WPF-Runtime).

## 6. Projektstruktur

| Option | Sonnet |
|---|---|
| Ein Projekt mit Ordnern | 5 |
| **Core-Library + App + Tests** | **9** |

**Entscheidung:** `ControllerMerger.Core` (reine Merge-/Deadzone-/Settings-Logik ohne Abhängigkeiten), `ControllerMerger` (WPF, SDL, ViGEm, Setup) und `ControllerMerger.Tests` (xUnit, 20 Tests).

## 7. Distribution

| Option | Opus | Sonnet | Haiku |
|---|---|---|---|
| **Self-contained Single-File** | **8** | **8** | **8** |
| Framework-dependent | 6 | 6 | 6 |

**Entscheidung (einstimmig): self-contained Single-File, win-x64, ReadyToRun, ohne Trimming** (WPF unterstützt weder Trimming noch AOT). Endnutzer brauchen keine .NET-Runtime.

## 8. Sicherheit der Treiberinstallation

**Einstimmig:** Version fest pinnen (nie „latest“), SHA-256 der exakten Datei pinnen, Authenticode-Signatur via `WinVerifyTrust` und den erwarteten Herausgeber (`CN=Nefarius Software Solutions e.U.`) prüfen. Download in ein frisches Temp-Verzeichnis, Start erst nach erfolgreicher Prüfung, Elevation nur für den Installer (die App selbst läuft `asInvoker`).

## 9. Top-Risiken & Gegenmaßnahmen

| Risiko | Gegenmaßnahme im Code |
|---|---|
| Feedback-Loop / Slot-Race beim Hotplug | Abschnitt 3: Quarantäne, Reihenfolge ViGEm → SDL, Slot-Neuabgleich |
| ViGEmBus EOL, durch Updates/HVCI blockierbar | Treiber hinter `VirtualPad` gekapselt; Statusprüfung beim Start; automatischer Reconnect alle 2 s; Reparatur-Assistent mit konkreten Lösungsschritten |
| Doppelte Eingaben im Spiel | Laut Nutzerentscheidung **kein HidHide**; stattdessen ein klarer Hinweis in den Einstellungen |
| HIDAPI-Konflikte mit Steam/DS4Windows | Dokumentiert in der README (Fehlerbehebung) |
| SmartScreen bei unsignierter EXE | Dokumentiert; Code-Signing ist für Releases empfohlen |

---

## Abgrenzung: Funktionsentscheidungen (vom Nutzer getroffen)

Laut Vorgabe entscheidet der Council nur über Architektur und Implementierung. Funktionsfragen wurden dem Nutzer vorgelegt:

| Frage | Entscheidung des Nutzers |
|---|---|
| Umgang mit Doppel-Eingaben (HidHide) | **Kein HidHide**, nur ein Hinweis |
| Stick-Merge-Modus | **Umschaltbar, Standard „pro Stick“** (zusätzlich „pro Achse“ wie ursprünglich spezifiziert) |
| Zusatzfunktionen | **Einzelne Controller pausieren** (keine Pro-Pad-Einstellungen, keine Live-Anzeige, keine Verbindungs-Benachrichtigungen) |
| UI-Sprache / PR | **Deutsch**, zunächst nur lokales Git; später: Projektname „K26K Controller Merger“, englische README, öffentliches GitHub-Repo mit Release |
