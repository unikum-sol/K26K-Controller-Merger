namespace ControllerMerger.Core;

/// <summary>Wie die Analog-Sticks mehrerer Controller kombiniert werden.</summary>
public enum StickMergeMode
{
    /// <summary>Der ganze Stick (X+Y) mit dem größten Ausschlag gewinnt. Standard – wirkt beim Helfen natürlich.</summary>
    PerStick = 0,

    /// <summary>Jede Achse einzeln: der Wert mit dem größten Absolutbetrag gewinnt.</summary>
    PerAxis = 1,
}

/// <summary>Achsen, deren Vorzeichen im Ausgang umgedreht wird.</summary>
[Flags]
public enum AxisInversion
{
    None = 0,
    LeftX = 1,
    LeftY = 2,
    RightX = 4,
    RightY = 8,
}

/// <summary>
/// Unveränderlicher Parametersatz für den Merge. Die UI ersetzt die Instanz atomar,
/// der Input-Thread liest pro Tick genau eine Referenz (keine Locks nötig).
/// </summary>
public sealed record MergeOptions
{
    /// <summary>Radiale Stick-Deadzone als Anteil 0..0.5 des Vollausschlags.</summary>
    public float StickDeadzone { get; init; } = 0.08f;

    /// <summary>Trigger-Schwelle als Anteil 0..0.5.</summary>
    public float TriggerDeadzone { get; init; } = 0.04f;

    public StickMergeMode StickMode { get; init; } = StickMergeMode.PerStick;

    public AxisInversion Inversion { get; init; } = AxisInversion.None;

    public static readonly MergeOptions Default = new();
}
