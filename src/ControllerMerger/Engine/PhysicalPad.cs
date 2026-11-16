using ControllerMerger.Core;
using ControllerMerger.Interop;

namespace ControllerMerger.Engine;

/// <summary>Ein geöffneter physischer Controller (nur vom Input-Thread benutzt).</summary>
internal sealed class PhysicalPad
{
    private const ushort VidNintendo = 0x057E;

    public required uint InstanceId { get; init; }
    public required nint Handle { get; init; }
    public required string Key { get; init; }
    public required string RawName { get; init; }
    public required PadKind Kind { get; init; }
    public required ConnectionKind Connection { get; init; }
    public required bool IsXInput { get; init; }
    public required int XInputSlot { get; init; }
    public required bool HasRumble { get; init; }
    public required ushort Vendor { get; init; }
    public required ushort Product { get; init; }

    /// <summary>SDL-Gerätepfad (HID-Schnittstellenpfad oder "XInput#n"); dient dem Verstecken über HidHide.</summary>
    public string? DevicePath { get; init; }

    public int BatteryPercent { get; set; } = -1;
    public bool Charging { get; set; }

    /// <summary>Ist das unser eigener virtueller Controller (oder noch nicht sicher ausgeschlossen)?</summary>
    public bool IsSelf { get; set; }

    public static unsafe PhysicalPad? Open(uint id)
    {
        nint h = Sdl.SDL_OpenGamepad(id);
        if (h == 0)
        {
            Services.Log.Warn($"SDL_OpenGamepad({id}) fehlgeschlagen: {Sdl.LastError}");
            return null;
        }

        string name = Sdl.PtrToString(Sdl.SDL_GetGamepadName(h)) ?? "Gamepad";
        string? path = Sdl.PtrToString(Sdl.SDL_GetGamepadPath(h));
        string? serial = Sdl.PtrToString(Sdl.SDL_GetGamepadSerial(h));
        var type = Sdl.SDL_GetGamepadType(h);
        ushort vid = Sdl.SDL_GetGamepadVendor(h), pid = Sdl.SDL_GetGamepadProduct(h);
        var guid = Sdl.SDL_GetJoystickGUID(Sdl.SDL_GetGamepadJoystick(h));
        string guidText = Sdl.GuidToString(guid);

        // SDL markiert Geräte des XInput-Backends mit 'x' an Byte 14 der GUID (SDL_IsJoystickXInput)
        // und vergibt den Pfad "XInput#<slot>".
        bool isXInput = guid.Data[14] == (byte)'x' ||
                        (path?.StartsWith("XInput#", StringComparison.OrdinalIgnoreCase) ?? false);
        int slot = -1;
        if (isXInput)
        {
            if (path is not null && path.StartsWith("XInput#", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(path.AsSpan(7), out var parsed))
                slot = parsed;
            else
                slot = Sdl.SDL_GetGamepadPlayerIndex(h);
        }

        var conn = Sdl.SDL_GetGamepadConnectionState(h) switch
        {
            Sdl.ConnectionState.Wired => ConnectionKind.Usb,
            Sdl.ConnectionState.Wireless => ConnectionKind.Bluetooth,
            _ => ConnectionKind.Unknown,
        };

        bool rumble = Sdl.SDL_GetBooleanProperty(Sdl.SDL_GetGamepadProperties(h), Sdl.PropGamepadCapRumble, false);

        var pad = new PhysicalPad
        {
            InstanceId = id,
            Handle = h,
            RawName = name,
            Kind = Classify(type, vid, pid, name),
            Connection = conn,
            IsXInput = isXInput,
            XInputSlot = slot,
            HasRumble = rumble,
            Vendor = vid,
            Product = pid,
            DevicePath = path,
            // Stabiler Schlüssel für "Eingaben übernehmen": Typ (GUID) + Seriennummer bzw. Gerätepfad.
            Key = $"{guidText}|{serial ?? path ?? ("slot" + slot)}",
        };
        pad.RefreshPower();
        return pad;
    }

    private static PadKind Classify(Sdl.GamepadType type, ushort vid, ushort pid, string name)
    {
        // Switch-2-Controller (Pro Controller 2, Joy-Con 2, NSO GameCube) – SDL 3.4 HIDAPI-Treiber, experimentell.
        if (vid == VidNintendo && pid is 0x2066 or 0x2067 or 0x2069 or 0x2073)
            return pid == 0x2073 ? PadKind.GameCube : PadKind.Switch2;
        if (name.Contains("Switch 2", StringComparison.OrdinalIgnoreCase)) return PadKind.Switch2;

        return type switch
        {
            Sdl.GamepadType.Xbox360 => PadKind.Xbox360,
            Sdl.GamepadType.XboxOne => PadKind.XboxOne,
            Sdl.GamepadType.Ps3 => PadKind.DualShock3,
            Sdl.GamepadType.Ps4 => PadKind.DualShock4,
            Sdl.GamepadType.Ps5 => PadKind.DualSense,
            Sdl.GamepadType.SwitchPro => PadKind.SwitchPro,
            Sdl.GamepadType.JoyConLeft or Sdl.GamepadType.JoyConRight or Sdl.GamepadType.JoyConPair => PadKind.JoyCon,
            Sdl.GamepadType.GameCube => PadKind.GameCube,
            _ => PadKind.Generic,
        };
    }

    public unsafe void RefreshPower()
    {
        int percent = -1;
        var state = Sdl.SDL_GetGamepadPowerInfo(Handle, &percent);
        Charging = state is Sdl.PowerState.Charging;
        BatteryPercent = state is Sdl.PowerState.OnBattery or Sdl.PowerState.Charging or Sdl.PowerState.Charged
            ? percent
            : -1;
    }

    /// <summary>
    /// Liest den aktuellen Zustand und rechnet ihn ins XInput-Format um.
    /// SDL-Buttons sind positionsbasiert (South = unten), also wird z. B. das Nintendo-"B" (unten) zu Xbox-"A".
    /// SDL-Y-Achsen zeigen nach unten, XInput nach oben → Vorzeichen drehen.
    /// </summary>
    public GamepadState Read()
    {
        nint h = Handle;
        PadButtons b = PadButtons.None;
        if (Sdl.SDL_GetGamepadButton(h, Sdl.GamepadButton.South)) b |= PadButtons.A;
        if (Sdl.SDL_GetGamepadButton(h, Sdl.GamepadButton.East)) b |= PadButtons.B;
        if (Sdl.SDL_GetGamepadButton(h, Sdl.GamepadButton.West)) b |= PadButtons.X;
        if (Sdl.SDL_GetGamepadButton(h, Sdl.GamepadButton.North)) b |= PadButtons.Y;
        if (Sdl.SDL_GetGamepadButton(h, Sdl.GamepadButton.Back)) b |= PadButtons.Back;
        if (Sdl.SDL_GetGamepadButton(h, Sdl.GamepadButton.Guide)) b |= PadButtons.Guide;
        if (Sdl.SDL_GetGamepadButton(h, Sdl.GamepadButton.Start)) b |= PadButtons.Start;
        if (Sdl.SDL_GetGamepadButton(h, Sdl.GamepadButton.LeftStick)) b |= PadButtons.LeftThumb;
        if (Sdl.SDL_GetGamepadButton(h, Sdl.GamepadButton.RightStick)) b |= PadButtons.RightThumb;
        if (Sdl.SDL_GetGamepadButton(h, Sdl.GamepadButton.LeftShoulder)) b |= PadButtons.LeftShoulder;
        if (Sdl.SDL_GetGamepadButton(h, Sdl.GamepadButton.RightShoulder)) b |= PadButtons.RightShoulder;
        if (Sdl.SDL_GetGamepadButton(h, Sdl.GamepadButton.DpadUp)) b |= PadButtons.DpadUp;
        if (Sdl.SDL_GetGamepadButton(h, Sdl.GamepadButton.DpadDown)) b |= PadButtons.DpadDown;
        if (Sdl.SDL_GetGamepadButton(h, Sdl.GamepadButton.DpadLeft)) b |= PadButtons.DpadLeft;
        if (Sdl.SDL_GetGamepadButton(h, Sdl.GamepadButton.DpadRight)) b |= PadButtons.DpadRight;

        return new GamepadState
        {
            Buttons = b,
            LeftX = Sdl.SDL_GetGamepadAxis(h, Sdl.GamepadAxis.LeftX),
            LeftY = MergeEngine.Negate(Sdl.SDL_GetGamepadAxis(h, Sdl.GamepadAxis.LeftY)),
            RightX = Sdl.SDL_GetGamepadAxis(h, Sdl.GamepadAxis.RightX),
            RightY = MergeEngine.Negate(Sdl.SDL_GetGamepadAxis(h, Sdl.GamepadAxis.RightY)),
            LeftTrigger = TriggerToByte(Sdl.SDL_GetGamepadAxis(h, Sdl.GamepadAxis.LeftTrigger)),
            RightTrigger = TriggerToByte(Sdl.SDL_GetGamepadAxis(h, Sdl.GamepadAxis.RightTrigger)),
        };
    }

    private static byte TriggerToByte(short v) => v <= 0 ? (byte)0 : (byte)((v * 255 + 16383) / 32767);

    public void Rumble(byte large, byte small)
    {
        if (!HasRumble) return;
        uint duration = (large | small) == 0 ? 0u : 1500u; // wird vor Ablauf aufgefrischt
        Sdl.SDL_RumbleGamepad(Handle, (ushort)(large * 257), (ushort)(small * 257), duration);
    }

    public void Close() => Sdl.SDL_CloseGamepad(Handle);

    public PadSnapshot ToSnapshot(bool enabled) => new(
        Key, InstanceId, PadNames.Friendly(Kind, RawName), RawName, Kind, Connection,
        BatteryPercent, Charging, HasRumble, enabled, XInputSlot, Vendor, Product, DevicePath, IsXInput);
}
