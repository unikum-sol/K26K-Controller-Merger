using System.Runtime.InteropServices;

namespace ControllerMerger.Interop;

/// <summary>
/// Schlanke, handgeschriebene SDL3-Bindung – nur die ~35 Funktionen, die das Tool braucht.
/// Entscheidung des Architektur-Councils: volle Kontrolle über Hints/Version, kein Fremd-Release-Takt.
/// Alle Funktionen dürfen ausschließlich vom Input-Thread aufgerufen werden.
/// SDL3 nutzt C99-<c>bool</c> (1 Byte) → <c>MarshalAs(U1)</c>.
/// </summary>
internal static unsafe partial class Sdl
{
    private const string Lib = "SDL3";

    public const uint InitGamepad = 0x00002000u;

    // Event-Typen (SDL_EventType)
    public const uint EventJoystickAxisMotion = 0x600;
    public const uint EventJoystickUpdateComplete = 0x608;
    public const uint EventGamepadAxisMotion = 0x650;
    public const uint EventGamepadButtonDown = 0x651;
    public const uint EventGamepadButtonUp = 0x652;
    public const uint EventGamepadAdded = 0x653;
    public const uint EventGamepadRemoved = 0x654;
    public const uint EventGamepadRemapped = 0x655;
    public const uint EventGamepadTouchpadDown = 0x656;
    public const uint EventGamepadUpdateComplete = 0x65A;

    // Hints
    public const string HintJoystickRawInput = "SDL_JOYSTICK_RAWINPUT";
    public const string HintJoystickWgi = "SDL_JOYSTICK_WGI";
    public const string HintJoystickHidapiXbox = "SDL_JOYSTICK_HIDAPI_XBOX";
    public const string HintJoystickAllowBackgroundEvents = "SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS";
    public const string HintXInputEnabled = "SDL_XINPUT_ENABLED";
    public const string HintJoystickHidapiSwitch2 = "SDL_JOYSTICK_HIDAPI_SWITCH2";
    public const string HintHidapiLibusb = "SDL_HIDAPI_LIBUSB";
    public const string HintCombineJoyCons = "SDL_JOYSTICK_HIDAPI_COMBINE_JOY_CONS";
    public const string HintPs5PlayerLed = "SDL_JOYSTICK_HIDAPI_PS5_PLAYER_LED";

    public const string PropGamepadCapRumble = "SDL.joystick.cap.rumble";

    public enum GamepadAxis { LeftX = 0, LeftY, RightX, RightY, LeftTrigger, RightTrigger }

    public enum GamepadButton
    {
        South = 0, East, West, North, Back, Guide, Start, LeftStick, RightStick,
        LeftShoulder, RightShoulder, DpadUp, DpadDown, DpadLeft, DpadRight, Misc1,
    }

    public enum GamepadType
    {
        Unknown = 0, Standard, Xbox360, XboxOne, Ps3, Ps4, Ps5, SwitchPro,
        JoyConLeft, JoyConRight, JoyConPair, GameCube,
    }

    public enum ConnectionState { Invalid = -1, Unknown = 0, Wired = 1, Wireless = 2 }

    public enum PowerState { Error = -1, Unknown = 0, OnBattery, NoBattery, Charging, Charged }

    [StructLayout(LayoutKind.Sequential)]
    public struct Guid16 { public fixed byte Data[16]; }

    /// <summary>SDL_Event ist eine 128-Byte-Union; uns interessieren nur Typ und Joystick-ID.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct Event
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(16)] public uint Which; // SDL_GamepadDeviceEvent.which
    }

    [LibraryImport(Lib)] public static partial int SDL_GetVersion();

    [LibraryImport(Lib)][return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SDL_Init(uint flags);

    [LibraryImport(Lib)] public static partial void SDL_Quit();

    [LibraryImport(Lib)] public static partial nint SDL_GetError();

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)][return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SDL_SetHint(string name, string value);

    [LibraryImport(Lib)] public static partial void SDL_PumpEvents();

    [LibraryImport(Lib)][return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SDL_PollEvent(Event* ev);

    [LibraryImport(Lib)]
    public static partial void SDL_SetEventEnabled(uint type, [MarshalAs(UnmanagedType.U1)] bool enabled);

    [LibraryImport(Lib)] public static partial void SDL_UpdateGamepads();

    [LibraryImport(Lib)] public static partial uint* SDL_GetGamepads(int* count);

    [LibraryImport(Lib)] public static partial void SDL_free(void* mem);

    [LibraryImport(Lib)] public static partial nint SDL_OpenGamepad(uint instanceId);

    [LibraryImport(Lib)] public static partial void SDL_CloseGamepad(nint gamepad);

    [LibraryImport(Lib)][return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SDL_GamepadConnected(nint gamepad);

    [LibraryImport(Lib)] public static partial short SDL_GetGamepadAxis(nint gamepad, GamepadAxis axis);

    [LibraryImport(Lib)][return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SDL_GetGamepadButton(nint gamepad, GamepadButton button);

    [LibraryImport(Lib)] public static partial nint SDL_GetGamepadName(nint gamepad);

    [LibraryImport(Lib)] public static partial nint SDL_GetGamepadPath(nint gamepad);

    [LibraryImport(Lib)] public static partial nint SDL_GetGamepadSerial(nint gamepad);

    [LibraryImport(Lib)] public static partial GamepadType SDL_GetGamepadType(nint gamepad);

    [LibraryImport(Lib)] public static partial int SDL_GetGamepadPlayerIndex(nint gamepad);

    [LibraryImport(Lib)] public static partial ushort SDL_GetGamepadVendor(nint gamepad);

    [LibraryImport(Lib)] public static partial ushort SDL_GetGamepadProduct(nint gamepad);

    [LibraryImport(Lib)] public static partial ConnectionState SDL_GetGamepadConnectionState(nint gamepad);

    [LibraryImport(Lib)] public static partial PowerState SDL_GetGamepadPowerInfo(nint gamepad, int* percent);

    [LibraryImport(Lib)] public static partial nint SDL_GetGamepadJoystick(nint gamepad);

    [LibraryImport(Lib)] public static partial Guid16 SDL_GetJoystickGUID(nint joystick);

    [LibraryImport(Lib)] public static partial void SDL_GUIDToString(Guid16 guid, byte* buffer, int size);

    [LibraryImport(Lib)] public static partial uint SDL_GetGamepadProperties(nint gamepad);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)][return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SDL_GetBooleanProperty(uint props, string name, [MarshalAs(UnmanagedType.U1)] bool defaultValue);

    [LibraryImport(Lib)][return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SDL_RumbleGamepad(nint gamepad, ushort low, ushort high, uint durationMs);

    public static string? PtrToString(nint p) => p == 0 ? null : Marshal.PtrToStringUTF8(p);

    public static string LastError => PtrToString(SDL_GetError()) ?? "unbekannter SDL-Fehler";

    public static string VersionString
    {
        get
        {
            int v = SDL_GetVersion();
            return $"{v / 1000000}.{v / 1000 % 1000}.{v % 1000}";
        }
    }

    public static string GuidToString(Guid16 guid)
    {
        byte* buf = stackalloc byte[33];
        SDL_GUIDToString(guid, buf, 33);
        return Marshal.PtrToStringUTF8((nint)buf) ?? "";
    }
}
