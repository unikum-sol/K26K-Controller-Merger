namespace ControllerMerger.Core;

/// <summary>
/// Kern des Tools: kombiniert beliebig viele Gamepad-Zustände zu einem.
/// Regeln:
/// <list type="bullet">
/// <item>Buttons: logisches ODER.</item>
/// <item>D-Pad: ODER, danach SOCD-Bereinigung – gegensätzliche Richtungen (z. B. oben+unten
///       von zwei Pads) heben sich auf, damit Spiele keinen ungültigen Zustand sehen.</item>
/// <item>Sticks: Wert mit größtem Absolutbetrag (pro Stick oder pro Achse, siehe <see cref="StickMergeMode"/>).</item>
/// <item>Trigger: Maximum.</item>
/// </list>
/// Die Deadzone wird VOR dem Merge pro Eingabe angewendet: so kann ein leicht driftender Stick
/// eines unbenutzten Pads nie gegen echte Eingaben gewinnen.
/// Alle Methoden sind allokationsfrei.
/// </summary>
public static class MergeEngine
{
    private const float StickMax = 32767f;

    /// <summary>Wendet Deadzones an und merged alle Eingaben in einem Durchlauf.</summary>
    public static GamepadState Merge(ReadOnlySpan<GamepadState> inputs, MergeOptions options)
    {
        var result = GamepadState.Neutral;
        long bestLeft = 0, bestRight = 0; // quadrierte Beträge für den Pro-Stick-Modus

        foreach (ref readonly var raw in inputs)
        {
            var s = ApplyDeadzone(raw, options.StickDeadzone, options.TriggerDeadzone);

            result.Buttons |= s.Buttons;
            if (s.LeftTrigger > result.LeftTrigger) result.LeftTrigger = s.LeftTrigger;
            if (s.RightTrigger > result.RightTrigger) result.RightTrigger = s.RightTrigger;

            if (options.StickMode == StickMergeMode.PerStick)
            {
                long l = (long)s.LeftX * s.LeftX + (long)s.LeftY * s.LeftY;
                if (l > bestLeft) { bestLeft = l; result.LeftX = s.LeftX; result.LeftY = s.LeftY; }

                long r = (long)s.RightX * s.RightX + (long)s.RightY * s.RightY;
                if (r > bestRight) { bestRight = r; result.RightX = s.RightX; result.RightY = s.RightY; }
            }
            else
            {
                result.LeftX = MaxAbs(result.LeftX, s.LeftX);
                result.LeftY = MaxAbs(result.LeftY, s.LeftY);
                result.RightX = MaxAbs(result.RightX, s.RightX);
                result.RightY = MaxAbs(result.RightY, s.RightY);
            }
        }

        result.Buttons = CleanDpad(result.Buttons);
        return ApplyInversion(result, options.Inversion);
    }

    /// <summary>Gibt den Wert mit dem größeren Absolutbetrag zurück (bei Gleichstand den ersten).</summary>
    public static short MaxAbs(short a, short b) => Math.Abs((int)b) > Math.Abs((int)a) ? b : a;

    /// <summary>Entfernt gleichzeitig gedrückte Gegenrichtungen (SOCD "neutral").</summary>
    public static PadButtons CleanDpad(PadButtons b)
    {
        const PadButtons vertical = PadButtons.DpadUp | PadButtons.DpadDown;
        const PadButtons horizontal = PadButtons.DpadLeft | PadButtons.DpadRight;
        if ((b & vertical) == vertical) b &= ~vertical;
        if ((b & horizontal) == horizontal) b &= ~horizontal;
        return b;
    }

    /// <summary>
    /// Radiale Stick-Deadzone mit Neuskalierung (kein "Sprung" am Rand der Deadzone) und
    /// Trigger-Schwelle mit Neuskalierung. Werte werden auf 0..0.5 begrenzt.
    /// </summary>
    public static GamepadState ApplyDeadzone(in GamepadState s, float stickDz, float triggerDz)
    {
        var r = s;
        ScaleStick(ref r.LeftX, ref r.LeftY, stickDz);
        ScaleStick(ref r.RightX, ref r.RightY, stickDz);
        r.LeftTrigger = ScaleTrigger(r.LeftTrigger, triggerDz);
        r.RightTrigger = ScaleTrigger(r.RightTrigger, triggerDz);
        return r;
    }

    private static void ScaleStick(ref short x, ref short y, float dz)
    {
        dz = Math.Clamp(dz, 0f, 0.5f);
        if (dz <= 0f) return;

        float fx = x / StickMax, fy = y / StickMax;
        float mag = MathF.Sqrt(fx * fx + fy * fy);
        if (mag <= dz) { x = 0; y = 0; return; }

        // Betrag von [dz..1] auf [0..1] abbilden, Richtung beibehalten.
        float clamped = MathF.Min(mag, 1f);
        float scale = (clamped - dz) / (1f - dz) / mag;
        x = ToShort(fx * scale * StickMax);
        y = ToShort(fy * scale * StickMax);
    }

    private static byte ScaleTrigger(byte v, float dz)
    {
        dz = Math.Clamp(dz, 0f, 0.5f);
        if (dz <= 0f) return v;
        float f = v / 255f;
        if (f <= dz) return 0;
        return (byte)Math.Clamp(MathF.Round((f - dz) / (1f - dz) * 255f), 0f, 255f);
    }

    /// <summary>Kehrt gewählte Achsen um (sättigend, -32768 wird zu 32767).</summary>
    public static GamepadState ApplyInversion(in GamepadState s, AxisInversion inv)
    {
        if (inv == AxisInversion.None) return s;
        var r = s;
        if ((inv & AxisInversion.LeftX) != 0) r.LeftX = Negate(r.LeftX);
        if ((inv & AxisInversion.LeftY) != 0) r.LeftY = Negate(r.LeftY);
        if ((inv & AxisInversion.RightX) != 0) r.RightX = Negate(r.RightX);
        if ((inv & AxisInversion.RightY) != 0) r.RightY = Negate(r.RightY);
        return r;
    }

    /// <summary>Sättigende Negation: -(-32768) passt nicht in short.</summary>
    public static short Negate(short v) => v == short.MinValue ? short.MaxValue : (short)-v;

    private static short ToShort(float v) => (short)Math.Clamp(MathF.Round(v), short.MinValue, short.MaxValue);
}
