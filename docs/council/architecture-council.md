# Architecture & implementation council

The council consisted of three Claude models with deliberately different perspectives. All three received the same problem statement and the same options and rated them independently. The results were then consolidated. On ties, the majority or the robustness perspective decided.

| Member | Perspective |
|---|---|
| Claude Opus | Robustness, latency, low-level correctness on Windows |
| Claude Sonnet | Maintainability, ecosystem, extensibility, developer experience |
| Claude Haiku | End-user experience, installation, resource usage, security |

**Environment at the start:** .NET 10 SDK available, no Rust toolchain, ViGEmBus v1.22.0 (project end-of-life/archived but functional), SDL 3.4.20 current.

---

## 1. Language / runtime

| Option | Opus | Sonnet | Haiku |
|---|---|---|---|
| **C# / .NET 10** | **8** | **9** | **9** |
| Rust (vigem-client + sdl3) | 5 | 4 | 5 |
| C++ | 5 | 3 | 6 |

**Decision: C# / .NET 10 (unanimous).**
Nefarius.ViGEm.Client is the reference implementation (fully managed), WPF is available natively, and the hot path can be written allocation-free, so the GC is irrelevant. Rust failed on the missing toolchain, a thin UI ecosystem for a high-quality interface, and a barely maintained ViGEm crate. C++ would have brought almost no latency gain at a high UI cost.

## 2. Input library

| Option | Opus | Sonnet | Haiku |
|---|---|---|---|
| **SDL3 (Gamepad API + HIDAPI)** | **9** | **9** | **9** |
| Windows.Gaming.Input | 4 | 4 | 5 |
| Custom HID parsers | 3 | 3 | 3 |
| Combination | 5 | 6 | – |

**Decision: SDL3 3.4.x (unanimous).** SDL covers XInput, DualShock 4, DualSense, Switch Pro, Joy-Cons, generic DirectInput/HID devices and Switch 2 controllers, including rumble, calibration, the mapping database and hot-plug. Maintenance is SDL's job. WGI doesn't support PlayStation/Switch pads and has background issues; custom parsers would be "a bottomless pit".

### Bindings

| Option | Opus | Sonnet |
|---|---|---|
| **Custom thin `LibraryImport` layer** | **8** | **8** |
| ppy.SDL3-CS | 7 | 7 |

**Decision: custom P/Invoke layer (`Interop/Sdl.cs`, ~35 functions)**, for full control over hints and version and no dependency on a third-party release cycle.
*Confirmed afterwards:* checking showed that the ppy.SDL3-CS NuGet package ships an **SDL 3.5.0 development build**. The official SDL **3.4.20** runtime is therefore pinned (`tools/fetch-sdl.ps1`, SHA-256 verified) and versioned in `lib/SDL3`.

## 3. Feedback-loop protection (critical)

To Windows, our own virtual Xbox 360 controller looks like a real XInput device. If it were read back in, a feedback loop would occur: buttons would "stick" and rumble would amplify itself.

| Option | Opus | Sonnet |
|---|---|---|
| (a) RawInput/WGI off, XInput backend, filter slot == ViGEm UserIndex | 8 | 6 |
| (b) Custom VID/PID | 6 | 7 |
| (c) Device path / parent enumerator | 6 | 8 |
| **(d) Combination** | **9** | **9** |

**Decision: combination, with (a) at its core:**
1. SDL hints `SDL_JOYSTICK_RAWINPUT=0`, `SDL_JOYSTICK_WGI=0`, `SDL_JOYSTICK_HIDAPI_XBOX=0`. Xbox pads are thus guaranteed to go through the XInput backend and have a unique slot (`XInput#n`, GUID marker `'x'`).
2. **Order:** connect ViGEm first, read its `UserIndex` (retry up to 1 s, then periodically), and only then start SDL.
3. **Quarantine:** while the slot is unknown, *all* XInput pads are ignored ("better to ignore briefly than to loop").
4. Rumble is never sent to the own slot.
5. After a driver reconnect, the slot is read again.

*Rejected:* (b), because a custom VID/PID endangers compatibility with games and Steam. The "loop detector" suggested by Sonnet (a pad mirroring the output exactly) was deliberately **not** implemented: a single real controller necessarily mirrors the output, which would cause false positives.
*Verified:* in the smoke test, the log showed the virtual controller taking XInput slot 1, and SDL reporting that very device as `Xbox 360 Controller (… XInput=True/0, self=True)`. The own pad is correctly detected and excluded.

## 4. UI framework & tray

| Option | Opus | Sonnet | Haiku |
|---|---|---|---|
| **WPF (custom styles, WindowChrome)** | **9** | **9** | **8** |
| WinUI 3 | 6 | 5 | 5 |
| Avalonia | 6 | 6 | 6 |
| WinForms | 3 | 3 | – |

| Tray | Opus | Sonnet | Haiku |
|---|---|---|---|
| **Hardcodet.NotifyIcon.Wpf** | 7 | **8** | **8** |
| Custom Shell_NotifyIcon wrapper | **8** | 5 | – |

**Decision: WPF with a custom design system, tray via Hardcodet (2:1).** The status popup is a separate borderless WPF window positioned pixel-precisely at the notification area via `SetWindowPos`. That meets Opus's main argument (popup positioning, dismiss on focus loss) with Hardcodet as well. Windows are created on demand and released on close (Sonnet), keeping idle RAM low.

## 5. Threading & timing

**Unanimously confirmed:**
- Dedicated input thread; SDL only on this thread; only `SDL_INIT_GAMEPAD`.
- `CreateWaitableTimerExW(CREATE_WAITABLE_TIMER_HIGH_RESOLUTION)` with an absolute, drift-free deadline; no global `timeBeginPeriod`.
- Thread registered with MMCSS as `"Games"` (`AvSetMmThreadCharacteristics`).
- Per tick: hot-plug events, read states, merge, **send a report only on change** (plus a keep-alive every 500 ms).
- The rumble callback (driver thread) only writes to an atomic mailbox; it is applied on the input thread and refreshed every second.
- The UI reads immutable snapshots (10 Hz); UI refresh only runs while a window is open.

*Measured:* 250 Hz at a 4 ms interval, **about 0.03 % CPU** when idle, about 76 MB working set (WPF runtime).

## 6. Project structure

| Option | Sonnet |
|---|---|
| One project with folders | 5 |
| **Core library + app + tests** | **9** |

**Decision:** `ControllerMerger.Core` (pure merge/dead-zone/settings logic without dependencies), `ControllerMerger` (WPF, SDL, ViGEm, setup) and `ControllerMerger.Tests` (xUnit, 20 tests).

## 7. Distribution

| Option | Opus | Sonnet | Haiku |
|---|---|---|---|
| **Self-contained single file** | **8** | **8** | **8** |
| Framework-dependent | 6 | 6 | 6 |

**Decision (unanimous): self-contained single file, win-x64, ReadyToRun, compressed, without trimming** (WPF supports neither trimming nor AOT). Users don't need a .NET runtime.

## 8. Security of the driver installation

**Unanimous:**
- Pin the version (never "latest").
- Pin the SHA-256 of the exact file.
- Check the Authenticode signature via `WinVerifyTrust` and the expected publisher (`CN=Nefarius Software Solutions e.U.`).
- Download into a fresh temp directory and run only after successful verification.
- Elevation only for the installer; the app itself runs `asInvoker`.

## 9. Top risks & mitigations

| Risk | Mitigation in code |
|---|---|
| Feedback loop / slot race on hot-plug | Section 3: quarantine, order ViGEm → SDL, slot re-check |
| ViGEmBus is end-of-life and may be blocked by updates/HVCI | Driver encapsulated in `VirtualPad`; status check at startup; automatic reconnect every 2 s; repair wizard with concrete steps |
| Duplicate input in games | Per the user's decision **no HidHide**; a clear hint in the settings instead |
| HIDAPI conflicts with Steam/DS4Windows | Documented in the README (troubleshooting) |
| SmartScreen for an unsigned EXE | Documented; code signing recommended for releases |

---

## Scope: feature decisions (made by the user)

As specified, the council only decided on architecture and implementation. Feature questions were put to the user:

| Question | User's decision |
|---|---|
| Handling of duplicate input (HidHide) | **No HidHide**, only a hint |
| Stick merge mode | **Switchable, default "whole stick"** (plus "per axis" as originally specified) |
| Extra features | **Pause individual controllers** (no per-pad settings, no live view, no connection notifications) |
| UI language / PR | **German UI**, initially local git only. Later: project name "K26K Controller Merger", English README and docs, public GitHub repo with release, **DE/EN language switch in the settings** |
