using ControllerMerger.Core;

namespace ControllerMerger.Engine;

/// <summary>Grobe Controller-Familie, abgeleitet aus SDL-Typ und VID/PID.</summary>
public enum PadKind
{
    Generic, Xbox360, XboxOne, DualShock3, DualShock4, DualSense, SwitchPro, Switch2, JoyCon, GameCube,
}

public enum ConnectionKind { Unknown, Usb, Bluetooth }

public enum EngineState
{
    /// <summary>Engine startet (Treiber verbinden, SDL initialisieren).</summary>
    Starting,
    /// <summary>Virtueller Controller aktiv, Eingaben werden zusammengeführt.</summary>
    Running,
    /// <summary>ViGEmBus nicht gefunden oder nicht erreichbar – automatischer Neuversuch läuft.</summary>
    DriverMissing,
    /// <summary>Anderer Fehler (z. B. SDL konnte nicht starten).</summary>
    Error,
    Stopped,
}

/// <summary>Unveränderliche Momentaufnahme eines physischen Controllers für die UI.</summary>
public sealed record PadSnapshot(
    string Key,
    uint InstanceId,
    string Name,
    string RawName,
    PadKind Kind,
    ConnectionKind Connection,
    int BatteryPercent,
    bool Charging,
    bool HasRumble,
    bool Enabled,
    int XInputSlot,
    ushort VendorId,
    ushort ProductId);

/// <summary>Unveränderliche Momentaufnahme der gesamten Engine (wird ~10× pro Sekunde erzeugt).</summary>
public sealed record EngineSnapshot(
    EngineState State,
    int VirtualSlot,
    IReadOnlyList<PadSnapshot> Pads,
    double RateHz,
    int IntervalMs,
    string SdlVersion,
    string? Message,
    GamepadState Output)
{
    public static readonly EngineSnapshot Initial =
        new(EngineState.Starting, -1, [], 0, 4, "–", null, GamepadState.Neutral);

    public int ActivePadCount => Pads.Count(p => p.Enabled);

    /// <summary>Struktureller Fingerabdruck: ändert sich nur, wenn sich für Tray/Popup Relevantes ändert.</summary>
    public string StatusSignature =>
        $"{State}|{VirtualSlot}|{Message}|" + string.Join(",", Pads.Select(p => $"{p.Key}:{p.Enabled}:{p.BatteryPercent / 10}"));
}

internal static class PadNames
{
    /// <summary>Gut lesbarer Produktname für die UI.</summary>
    public static string Friendly(PadKind kind, string rawName) => kind switch
    {
        PadKind.Xbox360 => "Xbox 360 Controller",
        PadKind.XboxOne => rawName.Contains("Series", StringComparison.OrdinalIgnoreCase)
            ? "Xbox Series Controller" : "Xbox One Controller",
        PadKind.DualShock3 => "DualShock 3",
        PadKind.DualShock4 => "DualShock 4",
        PadKind.DualSense => rawName.Contains("Edge", StringComparison.OrdinalIgnoreCase) ? "DualSense Edge" : "DualSense",
        PadKind.SwitchPro => "Switch Pro Controller",
        PadKind.Switch2 => rawName.Contains("Joy", StringComparison.OrdinalIgnoreCase) ? "Switch 2 Joy-Con" : "Switch 2 Pro Controller",
        PadKind.JoyCon => rawName,
        PadKind.GameCube => "GameCube Controller",
        _ => string.IsNullOrWhiteSpace(rawName) ? "Gamepad" : rawName,
    };

    /// <summary>Kurze Familienbezeichnung, z. B. für Untertitel.</summary>
    public static string Family(PadKind kind) => kind switch
    {
        PadKind.Xbox360 or PadKind.XboxOne => "Xbox",
        PadKind.DualShock3 or PadKind.DualShock4 or PadKind.DualSense => "PlayStation",
        PadKind.SwitchPro or PadKind.JoyCon => "Nintendo Switch",
        PadKind.Switch2 => UI.Loc.T("FamSwitch2"),
        PadKind.GameCube => "Nintendo",
        _ => "Gamepad",
    };

    /// <summary>Monogramm für die Kachel; leer = generisches Gamepad-Symbol.</summary>
    public static string Monogram(PadKind kind) => kind switch
    {
        PadKind.Xbox360 or PadKind.XboxOne => "XB",
        PadKind.DualShock3 or PadKind.DualShock4 or PadKind.DualSense => "PS",
        PadKind.SwitchPro or PadKind.JoyCon => "NS",
        PadKind.Switch2 => "NS2",
        PadKind.GameCube => "GC",
        _ => "",
    };
}
