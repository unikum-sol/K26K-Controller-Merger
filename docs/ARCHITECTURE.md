# Architecture

## Overview

```
┌──────────────────────────── K26KControllerMerger.exe (WPF, .NET 10) ───────────────────────┐
│                                                                                            │
│  UI thread                                    Input thread (MMCSS "Games", 1–8 ms)         │
│  ─────────                                    ────────────────────────────────────         │
│  App ─ TrayController (Hardcodet)             InputService                                 │
│   │     ├ right-click → StatusPopup            ├ VirtualPad ──► ViGEmBus (Xbox 360)        │
│   │     └ double-click → SettingsWindow        │     ▲ rumble callback (driver thread)     │
│   ├ SetupWindow (ViGEmBus wizard)              │     └─► atomic mailbox                    │
│   ├ MainViewModel ◄── EngineSnapshot (10 Hz) ──┤                                           │
│   ├ Loc (DE/EN, live switching)                │                                           │
│   └ SettingsStore ── MergeOptions (atomic) ───►├ SDL3 (custom P/Invoke layer)              │
│                                                │   XInput · HIDAPI (PS4/PS5/Switch) · DInput│
│                                                └ MergeEngine (ControllerMerger.Core)        │
└────────────────────────────────────────────────────────────────────────────────────────────┘
```

## Projects

| Project | Contents | Dependencies |
|---|---|---|
| `src/ControllerMerger.Core` | `GamepadState`, `MergeEngine` (OR, max deflection, dead zone, SOCD, inversion), `AppSettings` (JSON, source generator) | none |
| `src/ControllerMerger` | WPF app: engine, SDL interop, ViGEm, tray, windows, setup, design system, localization | Core, Nefarius.ViGEm.Client, Hardcodet.NotifyIcon.Wpf, SDL3.dll |
| `tests/ControllerMerger.Tests` | xUnit tests for merge logic and settings | Core |

## Data flow per tick

1. `SDL_UpdateGamepads` + `SDL_PumpEvents`: hot-plug events (`GAMEPAD_ADDED/REMOVED`) open or close devices. High-frequency state events are disabled; states are read directly.
2. For every active, non-paused pad that is **not** our own virtual controller, `PhysicalPad.Read()` converts SDL's representation (positional buttons, Y pointing down) to XInput format (Y pointing up, triggers 0–255).
3. `MergeEngine.Merge(span, options)`: dead zone per input, then buttons OR, triggers max, sticks largest deflection (per stick or per axis), D-pad with SOCD cleaning, and finally inversion.
4. A report is sent to ViGEm **only when it changes** (plus a keep-alive every 500 ms).
5. Rumble: when the mailbox changed, `SDL_RumbleGamepad` goes to all active pads (refreshed every 1 s, duration 1.5 s).
6. Every 100 ms an immutable `EngineSnapshot` is produced. If its status fingerprint changed, `StatusChanged` fires (used for the tray icon).

## Feedback-loop protection

To Windows, the virtual Xbox 360 controller is a real XInput device. Without filtering, it would be read back in.

- SDL hints `SDL_JOYSTICK_RAWINPUT=0`, `SDL_JOYSTICK_WGI=0`, `SDL_JOYSTICK_HIDAPI_XBOX=0` guarantee that Xbox devices go through the XInput backend (path `XInput#n`, GUID marker `'x'`).
- ViGEm connects first and its `UserIndex` is read; only then does SDL start.
- An XInput pad with `slot == UserIndex` is **self**. While the index is unknown, all XInput pads stay quarantined.
- Rumble is never sent to *self*.

## Timing

- `CreateWaitableTimerExW(CREATE_WAITABLE_TIMER_HIGH_RESOLUTION)` with an absolute deadline (no drift). If the thread falls far behind, it resynchronizes.
- Measured: **250 Hz at 4 ms**, about 0.03 % CPU when idle.
- The hot path is allocation-free (pre-allocated arrays, structs, `FrozenSet` for paused devices).

## Threading rules

| Resource | Access |
|---|---|
| SDL | input thread only |
| `MergeOptions`, interval, paused devices | `volatile` immutable objects, replaced by the UI |
| Rumble | driver thread → `Interlocked` mailbox → input thread |
| UI status | `EngineSnapshot` (immutable); UI timer at 4 Hz only while a window is open |
| Localization | immutable string table + volatile language flag; safe to read from any thread |

## Localization

- `UI/Strings.cs` holds every user-facing text in German and English. `UI/Loc.cs` provides `Loc.T(key)` / `Loc.F(key, args)` for code and the `{ui:T Key}` markup extension for XAML.
- Switching the language raises `Item[]` on the `Loc` singleton, so all XAML bindings update immediately without a restart. View models recompute their computed texts via `Loc.Changed`.
- The default follows the Windows display language (German if it's German, English otherwise). An explicit choice is stored in `settings.json`.

## Error handling

- Driver missing at startup → setup wizard (first run) or repair view with step-by-step solutions (later starts).
- Driver lost at runtime → state `DriverMissing`, automatic reconnect every 2 s, "Set up driver" in the popup and the overview.
- A failing tick is logged but never stops the engine.
- Unhandled exceptions are logged (`%LOCALAPPDATA%\K26KControllerMerger\log.txt`, rotated at 1 MB).
- Settings are saved atomically (temp file + replace). A corrupt file falls back to defaults.

## Extensibility

- **Other output backend** (ViGEmBus is end-of-life): `Engine/VirtualPad.cs` is the only place that touches the driver.
- **New merge rules:** `MergeEngine` is a pure, testable function; `MergeOptions` can grow new fields.
- **New controller families:** `PhysicalPad.Classify` + `PadNames` (display name, family, monogram).
- **Design:** new colors only as tokens in both theme files; controls only in `Themes/Controls.xaml`.
- **Another language:** extend the `(De, En)` tuple in `Strings.cs` with a third column, add a value to `UiLanguage`, and let `Loc.T` pick it.
