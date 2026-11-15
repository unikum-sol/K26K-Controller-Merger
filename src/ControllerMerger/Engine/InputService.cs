using System.Collections.Frozen;
using System.Diagnostics;
using ControllerMerger.Core;
using ControllerMerger.Interop;
using ControllerMerger.Services;
using Nefarius.ViGEm.Client.Exceptions;

namespace ControllerMerger.Engine;

/// <summary>
/// Die Merge-Engine. Läuft komplett auf EINEM dedizierten Thread:
/// <list type="number">
/// <item>ViGEm-Pad verbinden, dann dessen XInput-Slot ermitteln (Schutz vor Feedback-Loop).</item>
/// <item>SDL3 nur mit Gamepad-Subsystem starten; RawInput/WGI/HIDAPI-Xbox aus, damit Xbox-Pads
///       über das XInput-Backend laufen und der eigene Slot sicher erkennbar ist.</item>
/// <item>Pro Tick (1–8 ms, hochauflösender Waitable Timer, MMCSS "Games"): Hotplug-Events,
///       Zustände lesen, mergen, Report nur bei Änderung senden, Rumble weiterreichen.</item>
/// </list>
/// Der Hot-Path ist allokationsfrei. Die UI liest nur unveränderliche <see cref="EngineSnapshot"/>s.
/// </summary>
public sealed class InputService : IDisposable
{
    private const int MaxPads = 16;
    private const long SnapshotIntervalMs = 100;
    private const long PowerIntervalMs = 2000;
    private const long RumbleRefreshMs = 1000;
    private const long ReconnectIntervalMs = 2000;
    private const long SlotProbeIntervalMs = 100;
    /// <summary>Spätestens alle 500 ms den Zustand erneut senden (hält den Report frisch, kostet fast nichts).</summary>
    private const long KeepAliveMs = 500;

    private readonly Thread _thread;
    private volatile bool _stop;

    // Von der UI gesetzte Konfiguration – immer als ganze, unveränderliche Objekte ersetzt.
    private volatile MergeOptions _merge = MergeOptions.Default;
    private volatile int _intervalMs = 4;
    private volatile bool _rumbleEnabled = true;
    private volatile FrozenSet<string> _disabled = FrozenSet<string>.Empty;

    // Rumble-Mailbox: Treiber-Thread schreibt, Input-Thread liest. (large << 8 | small) + Sequenznummer.
    private int _rumbleValue;
    private int _rumbleSeq;

    private volatile EngineSnapshot _snapshot = EngineSnapshot.Initial;
    private string _lastSignature = "";

    // Nur vom Input-Thread benutzt:
    private readonly List<PhysicalPad> _pads = new(MaxPads);
    private readonly GamepadState[] _scratch = new GamepadState[MaxPads];
    private readonly VirtualPad _virtual = new();
    private EngineState _state = EngineState.Starting;
    private string? _message;
    private string _sdlVersion = "–";
    private bool _sdlReady;
    private int _virtualSlot = -1;
    private GamepadState _lastSent;
    private GamepadState _lastOutput;
    private bool _forceSend = true;

    /// <summary>Wird (auf dem Input-Thread!) ausgelöst, wenn sich der für Tray/Popup relevante Status ändert.</summary>
    public event Action<EngineSnapshot>? StatusChanged;

    public InputService()
    {
        _thread = new Thread(Run)
        {
            Name = "ControllerMerger.Input",
            IsBackground = true,
            Priority = ThreadPriority.AboveNormal,
        };
        _virtual.RumbleReceived += (large, small) =>
        {
            Volatile.Write(ref _rumbleValue, large << 8 | small);
            Interlocked.Increment(ref _rumbleSeq);
        };
    }

    public EngineSnapshot Snapshot => _snapshot;

    public void Start() => _thread.Start();

    /// <summary>Übernimmt Einstellungen threadsicher; wirkt ab dem nächsten Tick.</summary>
    public void ApplySettings(AppSettings s)
    {
        _merge = s.ToMergeOptions();
        _intervalMs = s.PollingIntervalMs;
        _rumbleEnabled = s.RumbleEnabled;
        _disabled = s.DisabledDevices.ToFrozenSet(StringComparer.Ordinal);
        Interlocked.Increment(ref _rumbleSeq); // Rumble ggf. sofort stoppen/anwenden
    }

    // ------------------------------------------------------------------ Thread

    private void Run()
    {
        nint mmcss = 0;
        nint timer = 0;
        try
        {
            uint taskIndex = 0;
            mmcss = Native.AvSetMmThreadCharacteristics("Games", ref taskIndex);
            timer = Native.CreateWaitableTimerEx(0, 0, Native.CreateWaitableTimerHighResolution, Native.TimerAllAccess);
            if (timer == 0) Log.Warn("Hochauflösender Timer nicht verfügbar – Fallback auf Thread.Sleep.");

            var clock = Stopwatch.StartNew();
            ConnectVirtual(clock);
            InitSdl();

            long freq = Stopwatch.Frequency;
            long next = clock.ElapsedTicks;
            long lastSnapshot = 0, lastPower = 0, lastRumble = 0, lastReconnect = 0, lastSlotProbe = 0, lastSend = 0;
            int ticks = 0, seenRumbleSeq = -1;
            long rateWindowStart = clock.ElapsedTicks;
            double rate = 0;
            bool rateLogged = false;

            while (!_stop)
            {
                long nowMs = clock.ElapsedMilliseconds;

                try
                {
                    // 1) Treiber verloren? → periodisch neu verbinden.
                    if (!_virtual.IsConnected && nowMs - lastReconnect >= ReconnectIntervalMs)
                    {
                        lastReconnect = nowMs;
                        ConnectVirtual(clock);
                    }

                    // 2) XInput-Slot des virtuellen Pads nachladen, solange unbekannt.
                    if (_virtual.IsConnected && _virtualSlot < 0 && nowMs - lastSlotProbe >= SlotProbeIntervalMs)
                    {
                        lastSlotProbe = nowMs;
                        UpdateVirtualSlot(_virtual.TryGetUserIndex());
                    }

                    if (_sdlReady)
                    {
                        // 3) SDL aktualisieren + Hotplug.
                        Sdl.SDL_UpdateGamepads();
                        PumpEvents();

                        // 4) Lesen + Mergen.
                        var disabled = _disabled;
                        int count = 0;
                        foreach (var pad in _pads)
                        {
                            if (pad.IsSelf || disabled.Contains(pad.Key) || count >= MaxPads) continue;
                            _scratch[count++] = pad.Read();
                        }

                        var output = MergeEngine.Merge(_scratch.AsSpan(0, count), _merge);
                        _lastOutput = output;

                        // 5) Senden – nur bei Änderung oder als seltenes Keep-Alive.
                        if (_virtual.IsConnected && (_forceSend || output != _lastSent || nowMs - lastSend >= KeepAliveMs))
                        {
                            try
                            {
                                _virtual.Submit(output);
                                _lastSent = output;
                                _forceSend = false;
                                lastSend = nowMs;
                            }
                            catch (Exception ex)
                            {
                                OnVirtualLost(ex);
                            }
                        }

                        // 6) Rumble weiterleiten (bei Änderung sofort, sonst periodisch auffrischen).
                        int seq = Volatile.Read(ref _rumbleSeq);
                        int value = Volatile.Read(ref _rumbleValue);
                        if (seq != seenRumbleSeq || (value != 0 && nowMs - lastRumble >= RumbleRefreshMs))
                        {
                            seenRumbleSeq = seq;
                            lastRumble = nowMs;
                            ApplyRumble(_rumbleEnabled ? value : 0);
                        }

                        // 7) Akkustände selten abfragen.
                        if (nowMs - lastPower >= PowerIntervalMs)
                        {
                            lastPower = nowMs;
                            foreach (var pad in _pads) pad.RefreshPower();
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Ein einzelner fehlerhafter Tick darf die Engine nie beenden.
                    Log.Error("Fehler im Input-Tick", ex);
                }

                // 8) Messung + Snapshot für die UI.
                ticks++;
                long nowTicks = clock.ElapsedTicks;
                if (nowTicks - rateWindowStart >= freq)
                {
                    rate = ticks * (double)freq / (nowTicks - rateWindowStart);
                    ticks = 0;
                    rateWindowStart = nowTicks;
                    if (!rateLogged && nowMs > 3000)
                    {
                        rateLogged = true;
                        Log.Info($"Abfragerate: {rate:0} Hz (Intervall {_intervalMs} ms, {(timer != 0 ? "High-Res-Timer" : "Sleep")}).");
                    }
                }
                if (nowMs - lastSnapshot >= SnapshotIntervalMs)
                {
                    lastSnapshot = nowMs;
                    PublishSnapshot(rate);
                }

                // 9) Bis zum nächsten Tick warten (driftfrei über absolute Deadline).
                long period = _intervalMs * freq / 1000;
                next += period;
                long remaining = next - clock.ElapsedTicks;
                if (remaining <= 0)
                {
                    if (-remaining > period * 4) next = clock.ElapsedTicks; // weit hinterher → neu synchronisieren
                    continue;
                }
                Wait(timer, remaining, freq);
            }
        }
        catch (Exception ex)
        {
            Log.Error("Input-Thread beendet", ex);
            _state = EngineState.Error;
            _message = ex.Message;
            PublishSnapshot(0);
        }
        finally
        {
            Shutdown();
            if (timer != 0) Native.CloseHandle(timer);
            if (mmcss != 0) Native.AvRevertMmThreadCharacteristics(mmcss);
        }
    }

    private static void Wait(nint timer, long remainingTicks, long freq)
    {
        if (timer != 0)
        {
            long due = -(remainingTicks * 10_000_000 / freq); // negativ = relativ, 100-ns-Einheiten
            if (Native.SetWaitableTimer(timer, due, 0, 0, 0, false))
            {
                Native.WaitForSingleObject(timer, Native.Infinite);
                return;
            }
        }
        Thread.Sleep(Math.Max(1, (int)(remainingTicks * 1000 / freq)));
    }

    // ------------------------------------------------------------------ Virtueller Controller

    private void ConnectVirtual(Stopwatch clock)
    {
        try
        {
            _virtual.Connect();
            _forceSend = true;
            Log.Info("Virtueller Xbox-360-Controller verbunden.");

            // Der Slot wird von Windows asynchron vergeben – kurz darauf warten (max. ~1 s), Rest im Tick.
            long until = clock.ElapsedMilliseconds + 1000;
            int slot;
            while ((slot = _virtual.TryGetUserIndex()) < 0 && clock.ElapsedMilliseconds < until && !_stop)
                Thread.Sleep(20);
            UpdateVirtualSlot(slot);

            if (_state is not EngineState.Error) _state = EngineState.Running;
            _message = slot < 0 ? "Warte auf XInput-Slot des virtuellen Controllers …" : null;
        }
        catch (VigemBusNotFoundException)
        {
            SetDriverMissing("ViGEmBus wurde nicht gefunden.");
        }
        catch (VigemBusVersionMismatchException)
        {
            SetDriverMissing("Die installierte ViGEmBus-Version ist nicht kompatibel.");
        }
        catch (VigemBusAccessFailedException)
        {
            SetDriverMissing("Kein Zugriff auf ViGEmBus (Treiber beschädigt oder blockiert).");
        }
        catch (Exception ex)
        {
            Log.Error("Virtueller Controller konnte nicht verbunden werden", ex);
            SetDriverMissing("Virtueller Controller konnte nicht erstellt werden: " + ex.Message);
        }
    }

    private void SetDriverMissing(string message)
    {
        _virtual.Dispose();
        UpdateVirtualSlot(-1);
        _state = EngineState.DriverMissing;
        _message = message;
    }

    private void OnVirtualLost(Exception ex)
    {
        Log.Error("Verbindung zum virtuellen Controller verloren", ex);
        SetDriverMissing("Verbindung zu ViGEmBus verloren – neuer Versuch läuft …");
    }

    private void UpdateVirtualSlot(int slot)
    {
        if (slot == _virtualSlot) return;
        _virtualSlot = slot;
        if (slot >= 0)
        {
            Log.Info($"Virtueller Controller belegt XInput-Slot {slot + 1}.");
            if (_state == EngineState.Running) _message = null;
        }
        foreach (var pad in _pads) pad.IsSelf = IsSelf(pad);
    }

    /// <summary>
    /// Feedback-Loop-Schutz: Ein XInput-Pad im Slot des virtuellen Controllers ist der virtuelle Controller.
    /// Solange der Slot unbekannt ist, bleiben ALLE XInput-Pads in Quarantäne (lieber kurz ignorieren als loopen).
    /// </summary>
    private bool IsSelf(PhysicalPad pad) => pad.IsXInput && (_virtualSlot < 0 || pad.XInputSlot == _virtualSlot);

    // ------------------------------------------------------------------ SDL

    private unsafe void InitSdl()
    {
        try
        {
            Sdl.SDL_SetHint(Sdl.HintJoystickRawInput, "0");
            Sdl.SDL_SetHint(Sdl.HintJoystickWgi, "0");
            Sdl.SDL_SetHint(Sdl.HintJoystickHidapiXbox, "0");
            Sdl.SDL_SetHint(Sdl.HintXInputEnabled, "1");
            Sdl.SDL_SetHint(Sdl.HintJoystickAllowBackgroundEvents, "1");
            Sdl.SDL_SetHint(Sdl.HintJoystickHidapiSwitch2, "1");
            Sdl.SDL_SetHint(Sdl.HintHidapiLibusb, "1");
            Sdl.SDL_SetHint(Sdl.HintCombineJoyCons, "1");

            if (!Sdl.SDL_Init(Sdl.InitGamepad))
            {
                _state = EngineState.Error;
                _message = UI.Loc.F("EngSdlFailed", Sdl.LastError);
                Log.Error(_message);
                return;
            }

            // Hochfrequente Zustands-Events abschalten – wir lesen Zustände direkt. Hotplug bleibt aktiv.
            foreach (uint t in (uint[])[0x600, 0x601, 0x602, 0x603, 0x604, 0x607, Sdl.EventJoystickUpdateComplete,
                         Sdl.EventGamepadAxisMotion, Sdl.EventGamepadButtonDown, Sdl.EventGamepadButtonUp,
                         Sdl.EventGamepadTouchpadDown, 0x657, 0x658, 0x659, Sdl.EventGamepadUpdateComplete])
                Sdl.SDL_SetEventEnabled(t, false);

            _sdlVersion = Sdl.VersionString;
            _sdlReady = true;
            Log.Info($"SDL {_sdlVersion} gestartet.");

            int count = 0;
            uint* ids = Sdl.SDL_GetGamepads(&count);
            if (ids != null)
            {
                for (int i = 0; i < count; i++) AddPad(ids[i]);
                Sdl.SDL_free(ids);
            }
        }
        catch (DllNotFoundException ex)
        {
            _state = EngineState.Error;
            _message = UI.Loc.T("EngSdlMissing");
            Log.Error(_message, ex);
        }
    }

    private unsafe void PumpEvents()
    {
        Sdl.SDL_PumpEvents();
        Sdl.Event ev;
        while (Sdl.SDL_PollEvent(&ev))
        {
            switch (ev.Type)
            {
                case Sdl.EventGamepadAdded: AddPad(ev.Which); break;
                case Sdl.EventGamepadRemoved: RemovePad(ev.Which); break;
            }
        }
    }

    private void AddPad(uint id)
    {
        foreach (var p in _pads) if (p.InstanceId == id) return;
        if (_pads.Count >= MaxPads) return;

        var pad = PhysicalPad.Open(id);
        if (pad is null) return;
        pad.IsSelf = IsSelf(pad);
        _pads.Add(pad);
        Interlocked.Increment(ref _rumbleSeq); // laufendes Rumble sofort auch auf das neue Pad geben
        Log.Info($"Controller verbunden: {pad.RawName} ({pad.Kind}, {pad.Connection}, XInput={pad.IsXInput}/{pad.XInputSlot}, self={pad.IsSelf})");
    }

    private void RemovePad(uint id)
    {
        for (int i = 0; i < _pads.Count; i++)
        {
            if (_pads[i].InstanceId != id) continue;
            var pad = _pads[i];
            _pads.RemoveAt(i);
            pad.Close();
            Log.Info($"Controller getrennt: {pad.RawName}");
            return;
        }
    }

    private void ApplyRumble(int value)
    {
        byte large = (byte)(value >> 8), small = (byte)value;
        var disabled = _disabled;
        foreach (var pad in _pads)
        {
            if (pad.IsSelf) continue; // niemals an den eigenen virtuellen Controller zurückgeben
            bool active = !disabled.Contains(pad.Key);
            pad.Rumble(active ? large : (byte)0, active ? small : (byte)0);
        }
    }

    // ------------------------------------------------------------------ Snapshot

    private void PublishSnapshot(double rate)
    {
        var disabled = _disabled;
        var pads = new List<PadSnapshot>(_pads.Count);
        foreach (var p in _pads)
            if (!p.IsSelf) pads.Add(p.ToSnapshot(!disabled.Contains(p.Key)));

        var state = _state == EngineState.Starting && _virtual.IsConnected && _sdlReady ? EngineState.Running : _state;
        var snap = new EngineSnapshot(state, _virtualSlot, pads, Math.Round(rate), _intervalMs, _sdlVersion, _message, _lastOutput);
        _snapshot = snap;

        var sig = snap.StatusSignature;
        if (sig != _lastSignature)
        {
            _lastSignature = sig;
            try { StatusChanged?.Invoke(snap); }
            catch (Exception ex) { Log.Error("StatusChanged-Handler", ex); }
        }
    }

    private void Shutdown()
    {
        try
        {
            ApplyRumble(0);
            foreach (var pad in _pads) pad.Close();
            _pads.Clear();
            if (_sdlReady) Sdl.SDL_Quit();
        }
        catch (Exception ex) { Log.Error("SDL-Shutdown", ex); }
        _virtual.Dispose();
        _state = EngineState.Stopped;
    }

    public void Dispose()
    {
        _stop = true;
        if (_thread.IsAlive && !_thread.Join(2000))
            Log.Warn("Input-Thread hat nicht rechtzeitig beendet.");
    }
}
