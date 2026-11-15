# Architektur

## Überblick

```
┌──────────────────────────── ControllerMerger.exe (WPF, .NET 10) ───────────────────────────┐
│                                                                                            │
│  UI-Thread                                    Input-Thread (MMCSS „Games“, 1–8 ms)         │
│  ─────────                                    ──────────────────────────────────────       │
│  App ─ TrayController (Hardcodet)             InputService                                 │
│   │     ├ Rechtsklick → StatusPopup            ├ VirtualPad ──► ViGEmBus (Xbox 360)        │
│   │     └ Doppelklick → SettingsWindow         │     ▲ Rumble-Callback (Treiber-Thread)    │
│   ├ SetupWindow (ViGEmBus-Assistent)           │     └─► atomare Mailbox                   │
│   ├ MainViewModel ◄── EngineSnapshot (10 Hz) ──┤                                           │
│   └ SettingsStore ── MergeOptions (atomar) ───►├ SDL3 (eigene P/Invoke-Schicht)             │
│                                                │   XInput · HIDAPI (PS4/PS5/Switch) · DInput│
│                                                └ MergeEngine (ControllerMerger.Core)        │
└────────────────────────────────────────────────────────────────────────────────────────────┘
```

## Projekte

| Projekt | Inhalt | Abhängigkeiten |
|---|---|---|
| `src/ControllerMerger.Core` | `GamepadState`, `MergeEngine` (OR, max. Ausschlag, Deadzone, SOCD, Inversion), `AppSettings` (JSON, Source-Generator) | keine |
| `src/ControllerMerger` | WPF-App: Engine, SDL-Interop, ViGEm, Tray, Fenster, Setup, Design-System | Core, Nefarius.ViGEm.Client, Hardcodet.NotifyIcon.Wpf, SDL3.dll |
| `tests/ControllerMerger.Tests` | xUnit-Tests für Merge-Logik und Einstellungen | Core |

## Datenfluss pro Tick

1. `SDL_UpdateGamepads` + `SDL_PumpEvents`: Hotplug-Events (`GAMEPAD_ADDED/REMOVED`) öffnen bzw. schließen Geräte. Hochfrequente Zustands-Events sind abgeschaltet, Zustände werden direkt gelesen.
2. Für jedes aktive, nicht pausierte Pad, das **nicht** der eigene virtuelle Controller ist, wird `PhysicalPad.Read()` aufgerufen. Das rechnet SDL (positionsbasierte Tasten, Y nach unten) ins XInput-Format um (Y nach oben, Trigger 0–255).
3. `MergeEngine.Merge(span, options)`: Deadzone pro Eingabe, danach Tasten per ODER, Trigger als Maximum, Sticks als größter Ausschlag (pro Stick oder pro Achse), D-Pad mit SOCD-Bereinigung, zuletzt Inversion.
4. Ein Report wird **nur bei Änderung** an ViGEm gesendet (plus Keep-Alive alle 500 ms).
5. Rumble: Ist die Mailbox geändert, wird `SDL_RumbleGamepad` an alle aktiven Pads gesendet (Refresh 1 s, Dauer 1,5 s).
6. Alle 100 ms entsteht ein unveränderlicher `EngineSnapshot`. Ändert sich der Status-Fingerabdruck, löst das `StatusChanged` aus (für das Tray-Symbol).

## Schutz vor dem Feedback-Loop

Der virtuelle Xbox-360-Controller ist für Windows ein echtes XInput-Gerät und würde ohne Filter wieder eingelesen. Die Maßnahmen:

- SDL-Hints `SDL_JOYSTICK_RAWINPUT=0`, `SDL_JOYSTICK_WGI=0`, `SDL_JOYSTICK_HIDAPI_XBOX=0`: Xbox-Geräte laufen damit garantiert über das XInput-Backend (Pfad `XInput#n`, GUID-Marker `'x'`).
- Zuerst wird ViGEm verbunden und der `UserIndex` gelesen, erst danach startet SDL.
- Ein XInput-Pad mit `slot == UserIndex` gilt als **self**. Solange der Index unbekannt ist, bleiben alle XInput-Pads in Quarantäne.
- Rumble wird nie an *self* gesendet.

## Timing

- `CreateWaitableTimerExW(CREATE_WAITABLE_TIMER_HIGH_RESOLUTION)` mit absoluter Deadline (driftfrei). Liegt der Thread weit zurück, wird neu synchronisiert.
- Gemessen: **250 Hz bei 4 ms**, ≈ 0,03 % CPU im Leerlauf.
- Der Hot-Path ist allokationsfrei (vorab allozierte Arrays, Structs, `FrozenSet` für pausierte Geräte).

## Threading-Regeln

| Ressource | Zugriff |
|---|---|
| SDL | ausschließlich Input-Thread |
| `MergeOptions`, Intervall, pausierte Geräte | `volatile`, unveränderliche Objekte, die von der UI ersetzt werden |
| Rumble | Treiber-Thread → `Interlocked`-Mailbox → Input-Thread |
| UI-Status | `EngineSnapshot` (unveränderlich), UI-Timer 4 Hz nur bei offenem Fenster |

## Fehlerbehandlung

- Treiber fehlt beim Start → Einrichtungsassistent (Ersteinrichtung) bzw. Reparaturansicht mit Lösungsschritten (spätere Starts).
- Treiber geht zur Laufzeit verloren → Zustand `DriverMissing`, automatischer Reconnect alle 2 s, „Treiber einrichten“ in Popup und Übersicht.
- Ein fehlerhafter Tick wird geloggt, beendet die Engine aber nie.
- Unbehandelte Ausnahmen werden geloggt (`%LOCALAPPDATA%\ControllerMerger\log.txt`, rotiert bei 1 MB).
- Einstellungen werden atomar gespeichert (temporäre Datei + Replace). Eine defekte Datei ergibt Standardwerte.

## Erweiterbarkeit

- **Anderes Ausgabe-Backend** (ViGEmBus ist EOL): `Engine/VirtualPad.cs` ist die einzige Stelle mit Treiberbezug.
- **Neue Merge-Regeln:** `MergeEngine` ist eine reine Funktion und testbar, `MergeOptions` lässt sich um weitere Felder ergänzen.
- **Neue Controller-Familien:** `PhysicalPad.Classify` + `PadNames` (Anzeigename, Familie, Monogramm).
- **Design:** neue Farben nur als Tokens in beiden Theme-Dateien, Controls nur in `Themes/Controls.xaml`.
