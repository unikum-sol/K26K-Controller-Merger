# K26K Controller Merger

**All your gamepads as one single Xbox 360 controller.** K26K Controller Merger permanently creates a virtual Xbox 360 controller through ViGEmBus and merges the input of every connected controller into it. It's ideal for helping a child on a second controller: both of you steer the same character at the same time.

![Overview](docs/design/screenshots/settings-overview-dark.png)

## Features

- **Always there:** the virtual Xbox 360 controller exists permanently, even when no physical controller is connected.
- **Fully automatic:** new controllers are detected via hot-plug and take effect immediately. No manual assignment needed.
- **True merging:**
  - Buttons: logical OR
  - Triggers: the stronger value wins
  - Analog sticks: largest deflection wins, either per *whole stick* (default) or per *individual axis*
  - D-pad: OR; opposite directions cancel each other out
  - Dead zone for sticks and triggers, applied per controller *before* merging, so drift on an idle controller never wins
- **Rumble** from games is forwarded to all active controllers.
- **Pause individual controllers:** they stay connected but no longer steer.
- **Supported controllers:** XInput (Xbox 360/One/Series), DualShock 4, DualSense, Switch Pro, Joy-Cons, generic DirectInput/HID gamepads, and Switch 2 controllers (experimental, see below).
- **Low latency:** polling every 1, 2, 4 (default) or 8 ms with a high-resolution timer, at about 0.03 % CPU when idle (measured).
- **Tray app:** right-click shows the status, double-click opens the settings. Light and dark mode follow Windows.
- **Autostart** can be turned on in the settings.

> The user interface is currently in German.

## Installation (users)

1. Download `K26KControllerMerger.exe` from the [latest release](../../releases/latest) and put it in a permanent location, e.g. `%LOCALAPPDATA%\Programs\K26K Controller Merger\`. No .NET installation is required.
2. Run it. On **first launch**, a setup wizard checks whether the **ViGEmBus** driver is installed. If it isn't, the wizard
   - downloads it from GitHub (pinned version **1.22.0**),
   - verifies it by **SHA-256 and digital signature** (Nefarius Software Solutions e.U.),
   - installs it with administrator rights (Windows will ask).
3. If Windows asks for a restart, K26K Controller Merger starts again automatically afterwards.
4. Done: the icon appears in the notification area of the taskbar.

If the driver is missing or broken on a later start, a repair view with step-by-step solutions opens.

## Usage

| Action | Result |
|---|---|
| **Right-click** the tray icon | Status popup: virtual controller, connected controllers, quit |
| **Double-click** the tray icon | Settings |
| Starting the EXE a second time | Opens the settings of the running instance |

**Tray icon:**
- Filled green dot: controllers are steering.
- Ring: ready, no active controller.
- Red dot with "!": a problem, e.g. a missing driver.

### Settings

| Page | Options |
|---|---|
| Overview (Übersicht) | Virtual controller status, "controlled by", list of all controllers with a switch to pause each one |
| Input (Eingabe) | Stick merge mode, dead zone (sticks/triggers), invert axes, polling interval |
| General (Allgemein) | Forward rumble, start with Windows, color scheme, technical details, help, log file |

Settings are stored in `%APPDATA%\K26KControllerMerger\settings.json`, the log in `%LOCALAPPDATA%\K26KControllerMerger\log.txt`.

## Building (developers)

**Requirements:** Windows 10/11 x64 and the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet build ControllerMerger.sln
```

```bash
dotnet test tests/ControllerMerger.Tests
```

```bash
dotnet publish src/ControllerMerger -c Release -o artifacts/publish
```

The result is `artifacts/publish/K26KControllerMerger.exe`, a self-contained single-file EXE (≈ 73 MB, compressed) with SDL3 included.

### Developer modes

```bash
K26KControllerMerger.exe --snapshot artifacts/snapshots
```
Renders every screen in light and dark mode to PNG files, without a driver or controllers. This is what the design council reviewed.

```bash
K26KControllerMerger.exe --export-icon src/ControllerMerger/Assets/AppIcon.ico
```
Regenerates the app icon.

### Updating SDL

SDL 3.4.20 (official release, zlib license) is vendored in `lib/SDL3`. To update, change `$Version` and `$Sha256` in `tools/fetch-sdl.ps1` and run:

```bash
powershell -ExecutionPolicy Bypass -File tools/fetch-sdl.ps1
```

## Troubleshooting

| Problem | Solution |
|---|---|
| **A game reacts twice** | The game sees the real controllers in addition to the virtual one. Select the virtual Xbox 360 controller in the game. If that doesn't help, hide the real controllers with [HidHide](https://github.com/nefarius/HidHide). |
| PlayStation/Switch controller not detected while Steam is running | Steam Input or DS4Windows can hold the device exclusively. Disable Steam Input for that device or close the other tool. |
| "Der Controller-Treiber läuft nicht" (driver not running) | Restart Windows. Then reinstall the driver from the wizard and check whether Memory Integrity or an antivirus is blocking it. |
| More than 3 Xbox controllers | XInput only has 4 slots, and the virtual controller uses one of them. |
| SmartScreen warning | The EXE is not code-signed. |

### Switch 2 controllers
SDL 3.4 includes a HIDAPI driver for Switch 2 controllers, and K26K Controller Merger enables it. According to the SDL release notes, wired support requires libusb. On Windows that means a `libusb-1.0.dll` next to the EXE and a WinUSB driver for the device. This is untested for lack of hardware, so support is **experimental**.

## Architecture and decisions

- [Architecture](docs/ARCHITECTURE.md): threads, data flow, feedback-loop protection, extensibility
- [Architecture council](docs/council/architecture-council.md): language, libraries, UI framework, distribution and security, with scores
- [Design council](docs/council/design-council.md): three iteration rounds until every member rated 9/10

The architecture and council documents are written in German.

**In short:**
- **C# / .NET 10 + WPF:** Nefarius.ViGEm.Client is the reference implementation, and WPF allows a fully custom design system.
- **SDL3 through a thin, custom P/Invoke layer:** the broadest controller support (HIDAPI for PlayStation and Switch) and full control over hints and version.
- **Dedicated input thread** with MMCSS, a high-resolution timer and an allocation-free hot path.
- **Feedback-loop protection:** the virtual controller's own XInput slot is detected and never read back.
- **Self-contained single file:** users need nothing but the EXE.

## License

K26K Controller Merger is licensed under the [GNU Affero General Public License v3.0](LICENSE).

### Third-party components

| Component | License |
|---|---|
| [SDL 3](https://libsdl.org) | zlib |
| [Nefarius.ViGEm.Client](https://github.com/nefarius/ViGEm.NET) | MIT |
| [Hardcodet.NotifyIcon.Wpf](https://github.com/hardcodet/wpf-notifyicon) | MIT |
| [ViGEmBus](https://github.com/nefarius/ViGEmBus) (installed at runtime, not bundled) | BSD-3-Clause |
