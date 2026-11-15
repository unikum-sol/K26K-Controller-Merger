using System.Runtime.InteropServices;

namespace ControllerMerger.Core;

/// <summary>
/// XInput-Buttonmaske. Die Bitwerte entsprechen exakt XINPUT_GAMEPAD_* bzw. dem
/// ViGEm-Xbox360-Report, sodass der gemergte Zustand ohne Umrechnung gesendet werden kann.
/// </summary>
[Flags]
public enum PadButtons : ushort
{
    None = 0,
    DpadUp = 0x0001,
    DpadDown = 0x0002,
    DpadLeft = 0x0004,
    DpadRight = 0x0008,
    Start = 0x0010,
    Back = 0x0020,
    LeftThumb = 0x0040,
    RightThumb = 0x0080,
    LeftShoulder = 0x0100,
    RightShoulder = 0x0200,
    Guide = 0x0400,
    A = 0x1000,
    B = 0x2000,
    X = 0x4000,
    Y = 0x8000,

    DpadMask = DpadUp | DpadDown | DpadLeft | DpadRight,
}

/// <summary>
/// Zustand eines Gamepads im XInput-Koordinatensystem:
/// Sticks -32768..32767 (Y positiv = oben), Trigger 0..255.
/// Bewusst ein kleines, blittables struct – der Input-Thread arbeitet allokationsfrei damit.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct GamepadState : IEquatable<GamepadState>
{
    public PadButtons Buttons;
    public short LeftX, LeftY, RightX, RightY;
    public byte LeftTrigger, RightTrigger;

    public static readonly GamepadState Neutral = default;

    public readonly bool IsNeutral => Equals(Neutral);

    public readonly bool Equals(GamepadState o) =>
        Buttons == o.Buttons && LeftX == o.LeftX && LeftY == o.LeftY &&
        RightX == o.RightX && RightY == o.RightY &&
        LeftTrigger == o.LeftTrigger && RightTrigger == o.RightTrigger;

    public override readonly bool Equals(object? obj) => obj is GamepadState s && Equals(s);

    public override readonly int GetHashCode() =>
        HashCode.Combine(Buttons, LeftX, LeftY, RightX, RightY, LeftTrigger, RightTrigger);

    public static bool operator ==(GamepadState a, GamepadState b) => a.Equals(b);
    public static bool operator !=(GamepadState a, GamepadState b) => !a.Equals(b);

    public override readonly string ToString() =>
        $"[{Buttons}] L({LeftX},{LeftY}) R({RightX},{RightY}) T({LeftTrigger},{RightTrigger})";
}
